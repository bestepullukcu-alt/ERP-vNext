using System.Text.Json;

namespace Diten.Web.Services.Branding;

public sealed class BrandingGateway : IBrandingGateway
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrandingGateway> _logger;

    public BrandingGateway(HttpClient httpClient, IConfiguration configuration, ILogger<BrandingGateway> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TenantBranding?> GetTenantBrandingAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var internalApiKey = _configuration["Platform:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(internalApiKey))
        {
            // No shared key configured — silently fall back to platform default branding.
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId}/branding");
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

            return data.Deserialize<TenantBranding>(JsonOptions);
        }
        catch (Exception ex)
        {
            // Branding is best-effort: never block login rendering on a branding failure.
            _logger.LogWarning(ex, "Failed to fetch tenant branding for {TenantId}; falling back to platform default.", tenantId);
            return null;
        }
    }
}
