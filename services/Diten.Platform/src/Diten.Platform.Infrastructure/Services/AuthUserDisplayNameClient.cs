using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class AuthUserDisplayNameClient : IUserDisplayNameResolver, IUserDisplayNameChecker
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>ATT-FIX2 — the client's OWN named HttpClient, never the default one (100 s): a name request that is
    /// not answered in <see cref="RequestTimeout"/> is a failed chunk (incomplete), not a held read.</summary>
    public const string HttpClientName = "auth-display-names";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Registers the named client with its timeout; AddInfrastructure calls this.</summary>
    public static IServiceCollection AddAuthDisplayNameHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, client => client.Timeout = RequestTimeout).WithoutRedirects();
        return services;
    }

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
        => (await ResolveCheckedAsync(userIds, ct)).Names;

    /// <summary>ATT-FIX1 E1 — the same resolution, saying whether every chunk was answered.</summary>
    public async Task<DisplayNameResolution> ResolveCheckedAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        var resolved = new Dictionary<Guid, string>();
        if (userIds is null || userIds.Count == 0)
        {
            return new DisplayNameResolution(resolved, Complete: true);
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
            return new DisplayNameResolution(resolved, Complete: true);
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogWarning(
                "Cannot resolve user display names; AuthService BaseUrl/InternalApiKey not configured. Names will be omitted.");
            return new DisplayNameResolution(resolved, Complete: false);
        }

        var complete = true;
        for (var offset = 0; offset < missing.Count; offset += ChunkSize)
        {
            var chunk = missing.Skip(offset).Take(ChunkSize).ToList();
            var (fetched, answered) = await FetchChunkAsync(tenantId, chunk, ct);
            complete &= answered;

            foreach (var entry in fetched)
            {
                resolved[entry.Key] = entry.Value;
                _cache.Set(CacheKey(tenantId, entry.Key), entry.Value, CacheDuration);
            }
        }

        return new DisplayNameResolution(resolved, complete);
    }

    private async Task<(IReadOnlyDictionary<Guid, string> Names, bool Answered)> FetchChunkAsync(
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

            // Its OWN named client (5 s timeout, ATT-FIX2), registered without redirects like every client that carries the
            // internal key (WP-EMAIL-SHELL-01 FIX3; see InternalHttpClients.AddAuthInternalHttpClients).
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthService display-name request failed. StatusCode={StatusCode} Count={Count}",
                    (int)response.StatusCode, chunk.Count);
                return (new Dictionary<Guid, string>(), false);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<List<AuthUserDisplayName>>(cancellationToken: ct);

            return payload is null
                ? (new Dictionary<Guid, string>(), false)
                : (payload
                    .Where(entry => entry.Id != Guid.Empty && !string.IsNullOrWhiteSpace(entry.DisplayName))
                    .GroupBy(entry => entry.Id)
                    .ToDictionary(group => group.Key, group => group.First().DisplayName), true);
        }
        catch (Exception ex)
        {
            // Best effort by contract: the assignee list and the work-item projection must still render, with
            // names simply absent, when AuthService is down.
            _logger.LogWarning(ex, "AuthService display-name request threw; names will be omitted for this batch.");
            return (new Dictionary<Guid, string>(), false);
        }
    }

    private static string CacheKey(Guid tenantId, Guid userId) => $"user-display-name:{tenantId}:{userId}";

    private sealed record AuthUserDisplayName(Guid Id, string DisplayName);
}
