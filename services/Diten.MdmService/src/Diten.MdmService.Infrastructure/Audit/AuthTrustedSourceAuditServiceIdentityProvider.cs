using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.Audit;

public sealed class AuthTrustedSourceAuditServiceIdentityProvider : ITrustedSourceAuditServiceIdentityProvider
{
    public const string ClientIdHeader = "X-Service-Client-Id";
    public const string ClientSecretHeader = "X-Service-Client-Secret";
    private const string IssuePath = "/api/internal/v1/auth/service-tokens/issue";
    private const int MaximumResponseBytes = 16 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthTrustedSourceAuditServiceIdentityProviderOptions _options;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<CacheKey, TrustedSourceAuditServiceIdentity> _cache = new();
    private readonly ConcurrentDictionary<CacheKey, Task<TrustedSourceAuditServiceIdentity>> _inflight = new();
    private readonly ConcurrentDictionary<CacheKey, RefreshCoordinator> _coordinators = new();

    public AuthTrustedSourceAuditServiceIdentityProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthTrustedSourceAuditServiceIdentityProviderOptions> options,
        TimeProvider clock)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<TrustedSourceAuditServiceIdentity> GetAsync(
        Guid tenantId,
        string audience,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        EnsureValidConfiguration(_options);
        PruneExpired();
        if (tenantId == Guid.Empty
            || !string.Equals(audience, AuditIntentDeliveryProcessor.RequiredAudience, StringComparison.Ordinal))
        {
            throw Terminal("AUDIT_SOURCE_IDENTITY_REQUEST_INVALID");
        }

        var key = new CacheKey(tenantId, audience, _options.ClientId, CredentialGeneration());
        var coordinator = RentCoordinator(key);
        try
        {
            var observedEpoch = Volatile.Read(ref coordinator.Epoch);
            if (forceRefresh)
            {
                var rejectedToken = _cache.TryGetValue(key, out var rejected) ? rejected.AccessToken : null;
                await coordinator.Gate.WaitAsync(cancellationToken);
                try
                {
                    if (_cache.TryGetValue(key, out var refreshed)
                        && IsReusable(refreshed)
                        && (rejectedToken is null
                            || !string.Equals(refreshed.AccessToken, rejectedToken, StringComparison.Ordinal)))
                    {
                        return refreshed;
                    }

                    var newEpoch = checked(observedEpoch + 1);
                    Volatile.Write(ref coordinator.Epoch, newEpoch);
                    _cache.TryRemove(key, out _);
                    _inflight.TryRemove(key, out _);
                    var identity = await AcquireAsync(tenantId, audience).WaitAsync(cancellationToken);
                    if (Volatile.Read(ref coordinator.Epoch) == newEpoch)
                    {
                        _cache[key] = identity;
                        ScheduleExpiry(key, identity);
                    }
                    return identity;
                }
                finally
                {
                    coordinator.Gate.Release();
                }
            }
            else if (_cache.TryGetValue(key, out var cached) && IsReusable(cached))
            {
                return cached;
            }

            var task = _inflight.GetOrAdd(key, _ => AcquireAsync(tenantId, audience));
            try
            {
                var identity = await task.WaitAsync(cancellationToken);
                if (Volatile.Read(ref coordinator.Epoch) == observedEpoch)
                {
                    _cache[key] = identity;
                    ScheduleExpiry(key, identity);
                }
                return identity;
            }
            finally
            {
                if (task.IsCompleted
                    && _inflight.TryGetValue(key, out var current)
                    && ReferenceEquals(current, task))
                {
                    _inflight.TryRemove(key, out _);
                }
            }
        }
        finally
        {
            ReturnCoordinator(key, coordinator);
        }
    }

    private async Task<TrustedSourceAuditServiceIdentity> AcquireAsync(Guid tenantId, string audience)
    {
        using var budget = new CancellationTokenSource(Budget);
        try
        {
            var active = await IssueAsync(tenantId, audience, _options.ActiveClientSecret, budget.Token);
            if (active.Identity is not null)
            {
                return active.Identity;
            }

            if (active.StatusCode == HttpStatusCode.Unauthorized && CanUsePreviousCredential())
            {
                var previous = await IssueAsync(
                    tenantId,
                    audience,
                    _options.PreviousClientSecret!,
                    budget.Token);
                if (previous.Identity is not null)
                {
                    return previous.Identity;
                }
                throw Classify(previous.StatusCode);
            }

            throw Classify(active.StatusCode);
        }
        catch (OperationCanceledException exception) when (budget.IsCancellationRequested)
        {
            throw new TrustedSourceAuditServiceIdentityException("AUDIT_SOURCE_IDENTITY_TIMEOUT", true, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new TrustedSourceAuditServiceIdentityException("AUDIT_SOURCE_IDENTITY_UNAVAILABLE", true, exception);
        }
    }

    private async Task<IssueAttempt> IssueAsync(
        Guid tenantId,
        string audience,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.AuthBaseUrl, IssuePath));
        request.Headers.TryAddWithoutValidation(ClientIdHeader, _options.ClientId);
        request.Headers.TryAddWithoutValidation(ClientSecretHeader, clientSecret);
        request.Content = JsonContent.Create(new { tenantId, audience });
        using var response = await _httpClientFactory.CreateClient(nameof(AuthTrustedSourceAuditServiceIdentityProvider))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new IssueAttempt(response.StatusCode, null);
        }
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");

        var bytes = await ReadBoundedAsync(response.Content, MaximumResponseBytes, cancellationToken);
        return new IssueAttempt(response.StatusCode, ParseIdentity(bytes, tenantId, audience));
    }

    private TrustedSourceAuditServiceIdentity ParseIdentity(byte[] utf8, Guid tenantId, string audience)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8, StrictDocumentOptions);
            var root = document.RootElement;
            RequireExactProperties(root, "data", "statusCode", "isSuccessful", "errors", "errorCodes");
            if (!root.GetProperty("isSuccessful").GetBoolean()
                || root.GetProperty("statusCode").GetInt32() != 200
                || root.GetProperty("errors").GetArrayLength() != 0
                || root.GetProperty("errorCodes").GetArrayLength() != 0)
            {
                throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
            }

            var data = root.GetProperty("data");
            RequireExactProperties(data, "accessToken", "tokenType", "expiresIn", "expiresAtUtc");
            var token = data.GetProperty("accessToken").GetString();
            var tokenType = data.GetProperty("tokenType").GetString();
            var expiresIn = data.GetProperty("expiresIn").GetInt32();
            var expiresAt = data.GetProperty("expiresAtUtc").GetDateTimeOffset();
            if (string.IsNullOrWhiteSpace(token) || token.Length > 8192
                || !string.Equals(tokenType, "Bearer", StringComparison.Ordinal)
                || expiresIn != 300 || expiresAt.Offset != TimeSpan.Zero)
            {
                throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
            }

            ValidateJwtFacts(token, tenantId, audience, expiresAt);
            var identity = new TrustedSourceAuditServiceIdentity(token, expiresAt);
            if (!IsReusable(identity))
            {
                throw Terminal("AUDIT_SOURCE_IDENTITY_EXPIRED");
            }
            return identity;
        }
        catch (TrustedSourceAuditServiceIdentityException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new TrustedSourceAuditServiceIdentityException(
                "AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID",
                false,
                exception);
        }
    }

    private void ValidateJwtFacts(string token, Guid tenantId, string audience, DateTimeOffset expiresAt)
    {
        var segments = token.Split('.');
        if (segments.Length != 3)
        {
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
        }
        using var header = JsonDocument.Parse(Base64UrlDecode(segments[0]), StrictDocumentOptions);
        RequireExactProperties(header.RootElement, "alg", "kid", "typ");
        if (!HasSingleExactString(header.RootElement, "alg", "RS256")
            || !HasSingleExactString(header.RootElement, "typ", "JWT")
            || !HasSingleBoundedString(header.RootElement, "kid", 128))
        {
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
        }
        using var payload = JsonDocument.Parse(Base64UrlDecode(segments[1]), StrictDocumentOptions);
        var root = payload.RootElement;
        RequireExactProperties(
            root,
            "sub",
            "actor_type",
            "service_name",
            "tenant_id",
            "jti",
            "iat",
            "nbf",
            "exp",
            "iss",
            "aud");
        var iat = ReadSingleInt64(root, "iat");
        var nbf = ReadSingleInt64(root, "nbf");
        var exp = ReadSingleInt64(root, "exp");
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (iat != nbf || exp - nbf != 300 || expiresAt.ToUnixTimeSeconds() != exp
            || nbf > now || exp <= now
            || !HasSingleExactString(root, "actor_type", "service")
            || !HasSingleExactString(root, "service_name", AuditIntentContract.SourceService)
            || !HasSingleExactString(root, "tenant_id", tenantId.ToString("D"))
            || !HasSingleBoundedString(root, "sub", 256)
            || !HasSingleBoundedString(root, "jti", 256)
            || !HasSingleExactString(root, "iss", _options.ExpectedIssuer)
            || !HasSingleGuid(root, "sub") || !HasSingleGuid(root, "jti")
            || !HasSingleExactAudience(root, audience))
        {
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
        }
    }

    private bool IsReusable(TrustedSourceAuditServiceIdentity identity) =>
        identity.ExpiresAtUtc > _clock.GetUtcNow().AddSeconds(_options.RefreshSkewSeconds);

    private bool CanUsePreviousCredential() =>
        !string.IsNullOrWhiteSpace(_options.PreviousClientSecret)
        && _options.PreviousClientSecretValidUntilUtc is { } validUntil
        && validUntil.Offset == TimeSpan.Zero
        && _clock.GetUtcNow() < validUntil;

    public static void EnsureValidConfiguration(AuthTrustedSourceAuditServiceIdentityProviderOptions options)
    {
        if (!Uri.TryCreate(options.AuthBaseUrl, UriKind.Absolute, out var uri)
            || !IsSecureInternalUri(uri)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/"
            || !IsExact(options.ExpectedIssuer, 256)
            || !IsExact(options.ClientId, 128)
            || !IsExact(options.ActiveClientSecret, 512)
            || options.RefreshSkewSeconds is < 1 or > 120
            || (options.PreviousClientSecret is null) != (options.PreviousClientSecretValidUntilUtc is null)
            || options.PreviousClientSecret is not null && !IsExact(options.PreviousClientSecret, 512)
            || options.PreviousClientSecretValidUntilUtc is { } previousUntil
               && previousUntil.Offset != TimeSpan.Zero)
        {
            throw Terminal("AUDIT_SOURCE_IDENTITY_CONFIGURATION_INVALID");
        }
    }

    private static bool IsSecureInternalUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;

    private string CredentialGeneration()
    {
        var material = string.Join('\n', _options.ClientId, _options.ActiveClientSecret,
            _options.PreviousClientSecret, _options.PreviousClientSecretValidUntilUtc?.ToString("O"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private void ScheduleExpiry(CacheKey key, TrustedSourceAuditServiceIdentity identity)
    {
        var delay = identity.ExpiresAtUtc - _clock.GetUtcNow();
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        _ = ExpireAsync(key, identity.AccessToken, delay);
    }

    private async Task ExpireAsync(CacheKey key, string token, TimeSpan delay)
    {
        await Task.Delay(delay, _clock, CancellationToken.None);
        if (_cache.TryGetValue(key, out var current)
            && string.Equals(current.AccessToken, token, StringComparison.Ordinal))
        {
            _cache.TryRemove(key, out _);
        }
        CleanupCoordinator(key);
    }

    private void PruneExpired()
    {
        var now = _clock.GetUtcNow();
        foreach (var entry in _cache)
        {
            if (entry.Value.ExpiresAtUtc <= now
                && _cache.TryGetValue(entry.Key, out var current)
                && ReferenceEquals(current, entry.Value))
            {
                _cache.TryRemove(entry.Key, out _);
                CleanupCoordinator(entry.Key);
            }
        }
    }

    private void CleanupCoordinator(CacheKey key)
    {
        if (_cache.ContainsKey(key) || _inflight.ContainsKey(key)) return;
        if (_coordinators.TryGetValue(key, out var coordinator)
            && coordinator.TryRetire())
        {
            _coordinators.TryRemove(new KeyValuePair<CacheKey, RefreshCoordinator>(key, coordinator));
        }
    }

    private RefreshCoordinator RentCoordinator(CacheKey key)
    {
        while (true)
        {
            var coordinator = _coordinators.GetOrAdd(key, _ => new RefreshCoordinator());
            if (!coordinator.TryRent())
            {
                _coordinators.TryRemove(new KeyValuePair<CacheKey, RefreshCoordinator>(key, coordinator));
                continue;
            }
            if (_coordinators.TryGetValue(key, out var current) && ReferenceEquals(current, coordinator))
                return coordinator;
            coordinator.Return();
        }
    }

    private void ReturnCoordinator(CacheKey key, RefreshCoordinator coordinator)
    {
        if (coordinator.Return() == 0) CleanupCoordinator(key);
    }

    private static TrustedSourceAuditServiceIdentityException Classify(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
            || (int)status >= 500
            ? new TrustedSourceAuditServiceIdentityException(
                status == HttpStatusCode.GatewayTimeout ? "AUDIT_SOURCE_IDENTITY_TIMEOUT" : "AUDIT_SOURCE_IDENTITY_UNAVAILABLE",
                true)
            : Terminal("AUDIT_SOURCE_IDENTITY_REJECTED");

    private static TrustedSourceAuditServiceIdentityException Terminal(string code) => new(code, false);

    private static Uri BuildUri(string baseUrl, string path) => new(new Uri(baseUrl, UriKind.Absolute), path);

    private static bool IsExact(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl) && !value.Contains(',', StringComparison.Ordinal);

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream(maximumBytes);
        var buffer = new byte[2048];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) return destination.ToArray();
            if (destination.Length + read > maximumBytes)
                throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void RequireExactProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
        var found = element.EnumerateObject().Select(item => item.Name).ToArray();
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(name => !found.Contains(name, StringComparer.Ordinal)))
            throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
    }

    private static long ReadSingleInt64(JsonElement element, string name)
    {
        var values = element.EnumerateObject().Where(item => item.NameEquals(name)).ToArray();
        return values.Length == 1 && values[0].Value.TryGetInt64(out var value)
            ? value
            : throw Terminal("AUDIT_SOURCE_IDENTITY_RESPONSE_INVALID");
    }

    private static bool HasSingleExactString(JsonElement element, string name, string expected)
    {
        var values = element.EnumerateObject().Where(item => item.NameEquals(name)).ToArray();
        return values.Length == 1 && values[0].Value.ValueKind == JsonValueKind.String
            && string.Equals(values[0].Value.GetString(), expected, StringComparison.Ordinal);
    }

    private static bool HasSingleBoundedString(JsonElement element, string name, int maximum)
    {
        var values = element.EnumerateObject().Where(item => item.NameEquals(name)).ToArray();
        var value = values.Length == 1 && values[0].Value.ValueKind == JsonValueKind.String
            ? values[0].Value.GetString()
            : null;
        return IsExact(value ?? string.Empty, maximum);
    }

    private static bool HasSingleGuid(JsonElement element, string name)
    {
        var values = element.EnumerateObject().Where(item => item.NameEquals(name)).ToArray();
        return values.Length == 1 && values[0].Value.ValueKind == JsonValueKind.String
            && Guid.TryParseExact(values[0].Value.GetString(), "D", out var value) && value != Guid.Empty;
    }

    private static bool HasSingleExactAudience(JsonElement element, string expected)
    {
        var values = element.EnumerateObject().Where(item => item.NameEquals("aud")).ToArray();
        if (values.Length != 1) return false;
        return values[0].Value.ValueKind == JsonValueKind.String
            ? string.Equals(values[0].Value.GetString(), expected, StringComparison.Ordinal)
            : values[0].Value.ValueKind == JsonValueKind.Array
              && values[0].Value.GetArrayLength() == 1
              && string.Equals(values[0].Value[0].GetString(), expected, StringComparison.Ordinal);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException() };
        return Convert.FromBase64String(padded);
    }

    private static readonly JsonDocumentOptions StrictDocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8
    };

    private sealed record CacheKey(Guid TenantId, string Audience, string ClientId, string CredentialGeneration);
    private sealed class RefreshCoordinator
    {
        private const int Retired = int.MinValue;
        private int _state;

        public readonly SemaphoreSlim Gate = new(1, 1);
        public long Epoch;

        public bool TryRent()
        {
            while (true)
            {
                var observed = Volatile.Read(ref _state);
                if (observed < 0) return false;
                if (observed == int.MaxValue)
                    throw new InvalidOperationException("AUDIT_SOURCE_IDENTITY_COORDINATOR_CAPACITY_EXCEEDED");
                if (Interlocked.CompareExchange(ref _state, observed + 1, observed) == observed)
                    return true;
            }
        }

        public int Return()
        {
            while (true)
            {
                var observed = Volatile.Read(ref _state);
                if (observed <= 0)
                    throw new InvalidOperationException("AUDIT_SOURCE_IDENTITY_COORDINATOR_LEASE_INVALID");
                if (Interlocked.CompareExchange(ref _state, observed - 1, observed) == observed)
                    return observed - 1;
            }
        }

        public bool TryRetire() => Interlocked.CompareExchange(ref _state, Retired, 0) == 0;
    }
    private sealed record IssueAttempt(HttpStatusCode StatusCode, TrustedSourceAuditServiceIdentity? Identity);
}
