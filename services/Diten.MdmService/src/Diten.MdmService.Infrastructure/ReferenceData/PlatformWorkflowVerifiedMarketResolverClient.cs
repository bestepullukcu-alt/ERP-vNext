using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

public sealed class PlatformWorkflowVerifiedMarketResolverClient
    : IWorkflowVerifiedMarketReferenceResolver
{
    public const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    public const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    public const string AudienceHeader = "X-Verified-Gsku-Audience";
    public const string ResolverAudience = "VERIFIED_GSKU_RESOLVE";
    private const string ResolvePath = "/api/internal/v1/reference-data/verified-market/resolve";
    private const int MaximumResponseBytes = 32 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _clients;
    private readonly IWorkflowVerifiedMarketServiceIdentityProvider _identities;
    private readonly VerifiedMarketResolverOptions _options;

    public PlatformWorkflowVerifiedMarketResolverClient(
        IHttpClientFactory clients,
        IWorkflowVerifiedMarketServiceIdentityProvider identities,
        IOptions<VerifiedMarketResolverOptions> options)
    {
        _clients = clients;
        _identities = identities;
        _options = options.Value;
    }

    public async Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
        Guid tenantId,
        string marketCode,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || !IsExactAlpha2(marketCode) || !IsConfigured())
            return Fail(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);
        try
        {
            var identity = await _identities.GetAsync(tenantId, false, budget.Token);
            var first = await SendOnceAsync(identity, marketCode, budget.Token);
            if (first.StatusCode != 401) return first.Result;

            identity = await _identities.GetAsync(tenantId, true, budget.Token);
            var replay = await SendOnceAsync(identity, marketCode, budget.Token);
            return replay.StatusCode == 401
                ? Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE")
                : replay.Result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(504, "REFERENCE_PROVIDER_TIMEOUT");
        }
        catch (WorkflowVerifiedMarketServiceIdentityException exception)
        {
            return Fail(
                exception.IsRetryable && exception.ErrorCode.EndsWith("TIMEOUT", StringComparison.Ordinal) ? 504 : 503,
                exception.IsRetryable && exception.ErrorCode.EndsWith("TIMEOUT", StringComparison.Ordinal)
                    ? "REFERENCE_PROVIDER_TIMEOUT"
                    : "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (InvalidDataException)
        {
            return Contract();
        }
        catch (IOException)
        {
            return Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE");
        }
    }

    private async Task<Attempt> SendOnceAsync(
        WorkflowVerifiedMarketServiceIdentity identity,
        string marketCode,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.PlatformBaseAddress!, ResolvePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.AccessToken);
        request.Headers.TryAddWithoutValidation(CredentialIdHeader, _options.CredentialIdentifier);
        request.Headers.TryAddWithoutValidation(CredentialSecretHeader, _options.CredentialSecret);
        request.Headers.TryAddWithoutValidation(AudienceHeader, ResolverAudience);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new Dictionary<string, string> { ["market_code"] = marketCode }),
            Encoding.UTF8,
            "application/json");

        using var response = await _clients
            .CreateClient(nameof(PlatformWorkflowVerifiedMarketResolverClient))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new(401, Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"));
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return new((int)response.StatusCode, MapTransportFailure(response.StatusCode));

        var payload = await ReadBoundedAsync(response.Content, cancellationToken);
        return new((int)response.StatusCode, Parse(response.StatusCode, payload, marketCode));
    }

    private static VerifiedMarketReferenceResolveResult Parse(
        HttpStatusCode status,
        byte[] payload,
        string marketCode)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, StrictOptions);
            var root = document.RootElement;
            RequireExact(root, "data", "statusCode", "isSuccessful", "errors", "reason_code", "correlation_id");
            if (root.GetProperty("statusCode").GetInt32() != (int)status
                || root.GetProperty("isSuccessful").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || root.GetProperty("errors").ValueKind != JsonValueKind.Array
                || root.GetProperty("correlation_id").ValueKind != JsonValueKind.String
                || !IsExact(root.GetProperty("correlation_id").GetString(), 256))
                return Contract();

            if (status == HttpStatusCode.OK)
            {
                if (!root.GetProperty("isSuccessful").GetBoolean()
                    || root.GetProperty("errors").GetArrayLength() != 0
                    || root.GetProperty("reason_code").ValueKind != JsonValueKind.Null
                    || root.GetProperty("data").ValueKind != JsonValueKind.Object)
                    return Contract();
                var data = root.GetProperty("data");
                RequireExact(data, "market");
                var market = data.GetProperty("market");
                RequireExact(market, "set_code", "value_code", "catalog_version_id", "catalog_version_number", "resolution_mode", "resolved_at_utc");
                var selection = new VerifiedMarketReferenceSelection(
                    market.GetProperty("set_code").GetString()!,
                    market.GetProperty("value_code").GetString()!,
                    market.GetProperty("catalog_version_id").GetGuid(),
                    market.GetProperty("catalog_version_number").GetInt32(),
                    market.GetProperty("resolution_mode").GetString()!,
                    market.GetProperty("resolved_at_utc").GetDateTimeOffset());
                return Trusted(selection, marketCode)
                    ? VerifiedMarketReferenceResolveResult.Success(selection)
                    : Contract();
            }

            if (root.GetProperty("isSuccessful").GetBoolean()
                || root.GetProperty("errors").GetArrayLength() == 0
                || root.GetProperty("data").ValueKind != JsonValueKind.Null)
                return Contract();
            var reason = root.GetProperty("reason_code").ValueKind == JsonValueKind.String
                ? root.GetProperty("reason_code").GetString()
                : null;
            if (!IsExact(reason, 160)) return Contract();
            return MapFailure(status, reason);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return Contract();
        }
    }

    private bool IsConfigured() => _options.PlatformBaseAddress is { IsAbsoluteUri: true } uri
        && uri.Scheme is "http" or "https" && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && IsExact(_options.CredentialIdentifier, 256) && IsExact(_options.CredentialSecret, 512);

    private static VerifiedMarketReferenceResolveResult MapFailure(HttpStatusCode status, string? reason) =>
        status switch
        {
            HttpStatusCode.Forbidden => Fail(403, "REFERENCE_PROVIDER_FORBIDDEN"),
            HttpStatusCode.NotFound => Fail(404, "REFERENCE_MARKET_NOT_FOUND"),
            HttpStatusCode.Conflict => Fail(409, "REFERENCE_CONTRACT_MISMATCH"),
            HttpStatusCode.GatewayTimeout => Fail(504, "REFERENCE_PROVIDER_TIMEOUT"),
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
                Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
            _ when (int)status >= 500 => Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
            _ => Contract()
        };

    private static VerifiedMarketReferenceResolveResult MapTransportFailure(HttpStatusCode status) =>
        status switch
        {
            HttpStatusCode.Forbidden => Fail(403, "REFERENCE_PROVIDER_FORBIDDEN"),
            HttpStatusCode.NotFound => Fail(404, "REFERENCE_MARKET_NOT_FOUND"),
            HttpStatusCode.Conflict => Fail(409, "REFERENCE_CONTRACT_MISMATCH"),
            HttpStatusCode.GatewayTimeout => Fail(504, "REFERENCE_PROVIDER_TIMEOUT"),
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable =>
                Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
            _ when (int)status >= 500 => Fail(503, "REFERENCE_PROVIDER_UNAVAILABLE"),
            _ => Contract()
        };

    private static bool Trusted(VerifiedMarketReferenceSelection selection, string marketCode) =>
        string.Equals(selection.SetCode, "market", StringComparison.Ordinal)
        && string.Equals(selection.ValueCode, marketCode, StringComparison.Ordinal)
        && selection.CatalogVersionId != Guid.Empty && selection.CatalogVersionNumber > 0
        && string.Equals(selection.ResolutionMode, "LATEST", StringComparison.Ordinal)
        && selection.ResolvedAtUtc != default && selection.ResolvedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsExactAlpha2(string? value) => value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z' && value[1] is >= 'A' and <= 'Z';
    private static bool IsExact(string? value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl) && !value.Contains(',');

    private static void RequireExact(JsonElement element, params string[] names)
    {
        var found = element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Select(x => x.Name).ToArray()
            : [];
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(x => !found.Contains(x, StringComparer.Ordinal)))
            throw new JsonException();
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes) throw new InvalidDataException();
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[2048];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaximumResponseBytes) throw new InvalidDataException();
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static VerifiedMarketReferenceResolveResult Contract() =>
        Fail(409, "REFERENCE_CONTRACT_MISMATCH");
    private static VerifiedMarketReferenceResolveResult Fail(int statusCode, string code) =>
        VerifiedMarketReferenceResolveResult.Fail(statusCode, code);

    private static readonly JsonDocumentOptions StrictOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8
    };

    private sealed record Attempt(int StatusCode, VerifiedMarketReferenceResolveResult Result);
}
