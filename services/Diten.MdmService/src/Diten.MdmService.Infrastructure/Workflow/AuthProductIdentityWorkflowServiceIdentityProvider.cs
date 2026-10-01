using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Workflow;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Workflow;

public sealed class AuthProductIdentityWorkflowServiceIdentityProvider : IProductIdentityWorkflowServiceIdentityProvider
{
    public const string Audience = "TRUSTED_WORKFLOW_CONSUMER";
    public const string ClientIdHeader = "X-Service-Client-Id";
    public const string ClientSecretHeader = "X-Service-Client-Secret";
    private const string ServiceName = "Diten.MDM";
    private const string IssuePath = "/api/internal/v1/auth/service-tokens/issue";
    private const int MaximumResponseBytes = 16 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _clients;
    private readonly AuthProductIdentityWorkflowServiceIdentityProviderOptions _options;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, ProductIdentityWorkflowServiceIdentity> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthProductIdentityWorkflowServiceIdentityProvider(
        IHttpClientFactory clients,
        IOptions<AuthProductIdentityWorkflowServiceIdentityProviderOptions> options,
        TimeProvider clock)
    {
        _clients = clients;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<ProductIdentityWorkflowServiceIdentity> GetAsync(
        Guid tenantId,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        EnsureValidConfiguration(_options);
        if (tenantId == Guid.Empty) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_REQUEST_INVALID");
        PruneExpired();
        if (!forceRefresh && _cache.TryGetValue(tenantId, out var cached) && IsReusable(cached)) return cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rejectedToken = forceRefresh && _cache.TryGetValue(tenantId, out var rejected)
                ? rejected.AccessToken
                : null;
            if (_cache.TryGetValue(tenantId, out cached) && IsReusable(cached)
                && (!forceRefresh || !string.Equals(cached.AccessToken, rejectedToken, StringComparison.Ordinal)))
            {
                return cached;
            }

            var acquired = await AcquireAsync(tenantId, cancellationToken);
            _cache[tenantId] = acquired;
            return acquired;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProductIdentityWorkflowServiceIdentity> AcquireAsync(Guid tenantId, CancellationToken callerToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        budget.CancelAfter(Budget);
        try
        {
            var active = await IssueAsync(tenantId, _options.ActiveClientSecret, budget.Token);
            if (active.Identity is not null) return active.Identity;
            if (active.Status == HttpStatusCode.Unauthorized && CanUsePrevious())
            {
                var previous = await IssueAsync(tenantId, _options.PreviousClientSecret!, budget.Token);
                if (previous.Identity is not null) return previous.Identity;
                throw Classify(previous.Status);
            }
            throw Classify(active.Status);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new ProductIdentityWorkflowServiceIdentityException("PRODUCT_WORKFLOW_IDENTITY_TIMEOUT", true);
        }
        catch (HttpRequestException exception)
        {
            throw new ProductIdentityWorkflowServiceIdentityException("PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE", true, exception);
        }
        catch (IOException exception)
        {
            throw new ProductIdentityWorkflowServiceIdentityException("PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE", true, exception);
        }
    }

    private async Task<IssueAttempt> IssueAsync(Guid tenantId, string secret, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.AuthBaseUrl), IssuePath));
        request.Headers.TryAddWithoutValidation(ClientIdHeader, _options.ClientId);
        request.Headers.TryAddWithoutValidation(ClientSecretHeader, secret);
        request.Content = JsonContent.Create(new { tenantId, audience = Audience });
        using var response = await _clients.CreateClient(nameof(AuthProductIdentityWorkflowServiceIdentityProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) return new(response.StatusCode, null);
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
        return new(response.StatusCode, Parse(await ReadBoundedAsync(response.Content, cancellationToken), tenantId));
    }

    private ProductIdentityWorkflowServiceIdentity Parse(byte[] utf8, Guid tenantId)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8, StrictOptions);
            var root = document.RootElement;
            RequireExact(root, "data", "statusCode", "isSuccessful", "errors", "errorCodes");
            if (!root.GetProperty("isSuccessful").GetBoolean() || root.GetProperty("statusCode").GetInt32() != 200
                || root.GetProperty("errors").GetArrayLength() != 0 || root.GetProperty("errorCodes").GetArrayLength() != 0)
                throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
            var data = root.GetProperty("data");
            RequireExact(data, "accessToken", "tokenType", "expiresIn", "expiresAtUtc");
            var token = data.GetProperty("accessToken").GetString();
            var expires = data.GetProperty("expiresAtUtc").GetDateTimeOffset();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 8192
                || !string.Equals(data.GetProperty("tokenType").GetString(), "Bearer", StringComparison.Ordinal)
                || data.GetProperty("expiresIn").GetInt32() != 300 || expires.Offset != TimeSpan.Zero)
                throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
            ValidateJwt(token, tenantId, expires);
            var identity = new ProductIdentityWorkflowServiceIdentity(token, expires);
            if (!IsReusable(identity)) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_EXPIRED");
            return identity;
        }
        catch (ProductIdentityWorkflowServiceIdentityException) { throw; }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            throw new ProductIdentityWorkflowServiceIdentityException("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID", false, exception);
        }
    }

    private void ValidateJwt(string token, Guid tenantId, DateTimeOffset expiresAt)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
        using var header = JsonDocument.Parse(Decode(parts[0]), StrictOptions);
        RequireExact(header.RootElement, "alg", "kid", "typ");
        if (!Exact(header.RootElement, "alg", "RS256") || !Exact(header.RootElement, "typ", "JWT")
            || !Bounded(header.RootElement, "kid", 128)) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
        using var payload = JsonDocument.Parse(Decode(parts[1]), StrictOptions);
        var root = payload.RootElement;
        RequireExact(root, "sub", "actor_type", "service_name", "tenant_id", "jti", "iat", "nbf", "exp", "iss", "aud");
        var iat = root.GetProperty("iat").GetInt64();
        var nbf = root.GetProperty("nbf").GetInt64();
        var exp = root.GetProperty("exp").GetInt64();
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (iat != nbf || exp - nbf != 300 || expiresAt.ToUnixTimeSeconds() != exp || nbf > now || exp <= now
            || !Exact(root, "actor_type", "service") || !Exact(root, "service_name", ServiceName)
            || !Exact(root, "tenant_id", tenantId.ToString("D")) || !Exact(root, "iss", _options.ExpectedIssuer)
            || !Exact(root, "aud", Audience) || !GuidValue(root, "sub") || !GuidValue(root, "jti"))
            throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
    }

    public static void EnsureValidConfiguration(AuthProductIdentityWorkflowServiceIdentityProviderOptions options)
    {
        if (!Uri.TryCreate(options.AuthBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || !IsExact(options.ExpectedIssuer, 256) || !IsExact(options.ClientId, 128)
            || !IsExact(options.ActiveClientSecret, 512) || options.RefreshSkewSeconds is < 1 or > 120
            || (options.PreviousClientSecret is null) != (options.PreviousClientSecretValidUntilUtc is null)
            || options.PreviousClientSecret is not null && !IsExact(options.PreviousClientSecret, 512)
            || options.PreviousClientSecretValidUntilUtc is { } previousUntil && previousUntil.Offset != TimeSpan.Zero)
            throw Terminal("PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID");
    }

    private bool IsReusable(ProductIdentityWorkflowServiceIdentity value) =>
        value.ExpiresAtUtc > _clock.GetUtcNow().AddSeconds(_options.RefreshSkewSeconds);
    private void PruneExpired()
    {
        var threshold = _clock.GetUtcNow().AddSeconds(_options.RefreshSkewSeconds);
        foreach (var item in _cache)
            if (item.Value.ExpiresAtUtc <= threshold) _cache.TryRemove(item.Key, out _);
    }
    private bool CanUsePrevious() => !string.IsNullOrWhiteSpace(_options.PreviousClientSecret)
        && _options.PreviousClientSecretValidUntilUtc is { } until && until.Offset == TimeSpan.Zero && _clock.GetUtcNow() < until;
    private static ProductIdentityWorkflowServiceIdentityException Classify(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
        || (int)status >= 500
            ? new(status == HttpStatusCode.GatewayTimeout ? "PRODUCT_WORKFLOW_IDENTITY_TIMEOUT" : "PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE", true)
            : Terminal("PRODUCT_WORKFLOW_IDENTITY_REJECTED");
    private static ProductIdentityWorkflowServiceIdentityException Terminal(string code) => new(code, false);
    private static bool IsExact(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl) && !value.Contains(',');
    private static void RequireExact(JsonElement value, params string[] names)
    {
        var found = value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Select(x => x.Name).ToArray() : [];
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(x => !found.Contains(x, StringComparer.Ordinal))) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
    }
    private static bool Exact(JsonElement value, string name, string expected) =>
        value.GetProperty(name).ValueKind == JsonValueKind.String && string.Equals(value.GetProperty(name).GetString(), expected, StringComparison.Ordinal);
    private static bool Bounded(JsonElement value, string name, int max) =>
        value.GetProperty(name).ValueKind == JsonValueKind.String && IsExact(value.GetProperty(name).GetString(), max);
    private static bool GuidValue(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String
        && Guid.TryParseExact(value.GetProperty(name).GetString(), "D", out var id) && id != Guid.Empty;
    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new FormatException() };
        return Convert.FromBase64String(padded);
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[2048];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaximumResponseBytes) throw Terminal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID");
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }
    private static readonly JsonDocumentOptions StrictOptions = new() { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8 };
    private sealed record IssueAttempt(HttpStatusCode Status, ProductIdentityWorkflowServiceIdentity? Identity);
}
