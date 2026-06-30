using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Diten.Web.Services.TenantStatus;

/// <summary>
/// Reads tenant liveness from Platform's internal S2S endpoint
/// (<c>GET /api/internal/tenants/{id}/status</c>, X-Internal-Api-Key). Mirrors
/// <see cref="Diten.Web.Services.Branding.BrandingGateway"/>'s best-effort pattern. Any failure returns
/// <c>null</c> (fail-open). Only DEFINITIVE answers are cached, briefly — a transport failure is never cached,
/// so recovery is picked up on the next request.
/// </summary>
public sealed class TenantStatusGateway : ITenantStatusGateway
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantStatusGateway> _logger;

    public TenantStatusGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<TenantStatusGateway> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TenantLiveness?> GetTenantLivenessAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var cacheKey = $"tenant-liveness:{tenantId:D}";
        if (_cache.TryGetValue(cacheKey, out TenantLiveness? cached) && cached is not null)
        {
            return cached;
        }

        var internalApiKey = _configuration["Platform:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(internalApiKey))
        {
            // Cannot verify without a key → fail-open (do not sign anyone out).
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/status");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, internalApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // 5xx / 401 / etc. — treat as "cannot verify" and fail-open. Not cached.
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object
                ? dataElement
                : root;

            if (data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("exists", out var existsEl)
                || !data.TryGetProperty("isActive", out var isActiveEl))
            {
                // Unexpected shape — fail-open rather than risk a wrongful sign-out.
                return null;
            }

            var liveness = new TenantLiveness(
                existsEl.ValueKind == JsonValueKind.True,
                isActiveEl.ValueKind == JsonValueKind.True);

            _cache.Set(cacheKey, liveness, CacheTtl);
            return liveness;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant liveness check failed for {TenantId}; failing open.", tenantId);
            return null;
        }
    }
}
