using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Diten.Web.Services.TenantResolution;

public interface ITenantSlugResolver
{
    // Returns the tenant id for an active tenant matching the slug, or null when the slug is
    // unknown/inactive/unresolvable. Best-effort: never throws.
    Task<Guid?> ResolveActiveTenantIdAsync(string slug, CancellationToken ct = default);
}

// Translates a public tenant slug (e.g. "gmg") into its tenant id so the Web app can send a
// vanity URL like http://<host>/gmg to the tenant login screen. Targets the Platform service
// DIRECTLY with the shared internal API key (same pattern as BrandingGateway) — the internal
// endpoint is not exposed through the gateway. Results are short-cached to avoid a Platform
// round-trip on every unmatched single-segment request.
public sealed class TenantSlugResolver : ITenantSlugResolver
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private static readonly TimeSpan HitTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MissTtl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantSlugResolver> _logger;

    public TenantSlugResolver(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<TenantSlugResolver> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Guid?> ResolveActiveTenantIdAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var cacheKey = $"tenant-slug::{normalized}";
        if (_cache.TryGetValue<Guid?>(cacheKey, out var cached))
        {
            return cached;
        }

        var resolved = await ResolveFromPlatformAsync(normalized, ct);
        _cache.Set(cacheKey, resolved, resolved.HasValue ? HitTtl : MissTtl);
        return resolved;
    }

    private async Task<Guid?> ResolveFromPlatformAsync(string slug, CancellationToken ct)
    {
        var internalApiKey = _configuration["Platform:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(internalApiKey))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/by-slug/{Uri.EscapeDataString(slug)}");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, internalApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object
                ? dataElement
                : root;

            var isActive = data.TryGetProperty("isActive", out var activeElement) && activeElement.ValueKind == JsonValueKind.True;
            if (!isActive)
            {
                return null;
            }

            if (data.TryGetProperty("tenantId", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(idElement.GetString(), out var tenantId) &&
                tenantId != Guid.Empty)
            {
                return tenantId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant slug '{Slug}' via Platform.", slug);
            return null;
        }
    }
}
