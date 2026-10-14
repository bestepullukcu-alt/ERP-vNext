using System.Security.Claims;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Infrastructure.ReferenceData;

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — the active codes of a business reference set through the Platform consumable-sets route
/// (<c>/api/lookups/reference-data/consumable-sets/{set}/published-values</c>) on the Gateway, with the CALLER's
/// <c>Authorization</c> + <c>X-Tenant-Id</c> (the Platform reads the set by its own scope — <c>mdm-reimbursement-status</c> is
/// a tenant set). Any failure → null (the caller answers 503, nothing is saved).
/// </summary>
public sealed class GatewayReferenceValueReader : IMdmReferenceValueReader
{
    private const string PathTemplate = "/api/lookups/reference-data/consumable-sets/{0}/published-values";

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GatewayReferenceValueReader> _logger;

    public GatewayReferenceValueReader(HttpClient http, IConfiguration configuration, IHttpContextAccessor httpContextAccessor,
        ILogger<GatewayReferenceValueReader> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        var baseUrl = configuration["Gateway:BaseUrl"];
        _http.BaseAddress ??= new Uri(string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:5000" : baseUrl);
    }

    public async Task<IReadOnlySet<string>?> ActiveCodesAsync(string setCode, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(PathTemplate, Uri.EscapeDataString(setCode)));
            var context = _httpContextAccessor.HttpContext;
            if (context?.Request.Headers.TryGetValue("Authorization", out var auth) == true)
            {
                request.Headers.TryAddWithoutValidation("Authorization", auth.ToString());
            }

            var tenant = context?.Request.Headers.TryGetValue("X-Tenant-Id", out var header) == true ? header.ToString()
                : context?.User.FindFirstValue("tenant_id") ?? context?.User.FindFirstValue("tenantId");
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
            }

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("mdm.reference_set.read_failed Set={Set} Status={Status}", setCode, (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            var items = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var i) ? i : data;
            if (items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if ((item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                    || (item.TryGetProperty("isDeprecated", out var dep) && dep.ValueKind == JsonValueKind.True)) continue;
                var code = Text(item, "valueCode") ?? Text(item, "value_code") ?? Text(item, "code");
                if (code is not null) codes.Add(code);
            }

            return codes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "mdm.reference_set.read_failed Set={Set}", setCode);
            return null;
        }
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
