using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// S2S display-name resolver (MOD-0024 §K6.4 / DEV-2). Mirrors <see cref="AuthPermissionModulesClient"/>:
/// same X-Internal-Api-Key header, same AuthService:BaseUrl, same never-throw contract.
///
/// <para><b>Call shape.</b> One request per chunk of <see cref="ChunkSize"/> ids, not one per user — resolving 50
/// people is a single round trip, and repeats inside the cache window are zero. Names change rarely, so a short
/// memory cache is safe and turns the common "render the list twice" case into no traffic at all.</para>
///
/// <para><b>Tenant safety.</b> The tenant id is taken from the server-side <see cref="ITenantContext"/>, never
/// from a caller-supplied value, and AuthService scopes the lookup by it again. Cache keys include the tenant so
/// one tenant's names can never be served to another.</para>
/// </summary>
public sealed class AuthUserDisplayNameClient : IUserDisplayNameResolver
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>Ids per request. Keeps the query string bounded while staying far from one-call-per-user.</summary>
    private const int ChunkSize = 100;

    /// <summary>Names are near-static; a short window keeps a stale rename visible for minutes at most.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ITenantContext _tenantContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthUserDisplayNameClient> _logger;

    public AuthUserDisplayNameClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ITenantContext tenantContext,
        IMemoryCache cache,
        ILogger<AuthUserDisplayNameClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _tenantContext = tenantContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        var resolved = new Dictionary<Guid, string>();
        if (userIds is null || userIds.Count == 0)
        {
            return resolved;
        }

        var tenantId = _tenantContext.TenantId;
        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();

        // Serve what the cache already holds; only the remainder costs a request.
        var missing = new List<Guid>();
        foreach (var id in wanted)
        {
            if (_cache.TryGetValue(CacheKey(tenantId, id), out string? cached) && cached is not null)
            {
                resolved[id] = cached;
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count == 0)
        {
            return resolved;
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogWarning(
                "Cannot resolve user display names; AuthService BaseUrl/InternalApiKey not configured. Names will be omitted.");
            return resolved;
        }

        for (var offset = 0; offset < missing.Count; offset += ChunkSize)
        {
            var chunk = missing.Skip(offset).Take(ChunkSize).ToList();
            var fetched = await FetchChunkAsync(tenantId, chunk, ct);

            foreach (var entry in fetched)
            {
                resolved[entry.Key] = entry.Value;
                _cache.Set(CacheKey(tenantId, entry.Key), entry.Value, CacheDuration);
            }
        }

        return resolved;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> FetchChunkAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> chunk,
        CancellationToken ct)
    {
        try
        {
            var url = $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/users/display-names"
                      + $"?tenantId={tenantId}&ids={string.Join(',', chunk)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthService display-name request failed. StatusCode={StatusCode} Count={Count}",
                    (int)response.StatusCode, chunk.Count);
                return new Dictionary<Guid, string>();
            }

            var payload = await response.Content
                .ReadFromJsonAsync<List<AuthUserDisplayName>>(cancellationToken: ct);

            return payload is null
                ? new Dictionary<Guid, string>()
                : payload
                    .Where(entry => entry.Id != Guid.Empty && !string.IsNullOrWhiteSpace(entry.DisplayName))
                    .GroupBy(entry => entry.Id)
                    .ToDictionary(group => group.Key, group => group.First().DisplayName);
        }
        catch (Exception ex)
        {
            // Best effort by contract: the assignee list and the work-item projection must still render, with
            // names simply absent, when AuthService is down.
            _logger.LogWarning(ex, "AuthService display-name request threw; names will be omitted for this batch.");
            return new Dictionary<Guid, string>();
        }
    }

    private static string CacheKey(Guid tenantId, Guid userId) => $"user-display-name:{tenantId}:{userId}";

    private sealed record AuthUserDisplayName(Guid Id, string DisplayName);
}
