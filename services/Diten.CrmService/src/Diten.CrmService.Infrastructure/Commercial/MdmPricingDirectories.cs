using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — product-master v1.1 (<c>/api/product-master</c>, docs/analysis/contracts/product-master-bundle.openapi.yaml)
/// through the Gateway with the caller's token (the CRM MDM client pattern). FAIL CLOSED: any timeout / error status /
/// unreadable answer is null ("could not ask" → 503 upstream), never "does not exist".
/// <list type="bullet">
/// <item><see cref="ReadAsync"/> → <c>POST /skus/bulk-read</c> (≤ 200 ids a call; Retired included with its status).</item>
/// <item><see cref="FindByCodesAsync"/> → <c>GET /stockable-items?q={code}</c> (a code PREFIX search; only the exact canonical
/// code is taken), once without and once with <c>status=Retired</c> (the default search leaves Retired out — the import must
/// see it to mark it).</item>
/// <item><see cref="FindByGtinsAsync"/> → v1.1 has NO GTIN search (BL-584 W-04, v1.2): an empty answer — the rows stay
/// unmatched in the preview (§20.3 AÇIK-5); the need is reported to the MDM owner.</item>
/// </list>
/// </summary>
public sealed class MdmPricingSkuDirectory : IPricingSkuDirectory
{
    private const string BasePath = "api/product-master";
    private const int BulkLimit = 200;
    private const int MaxCodeLookups = 2000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ITenantContext _tenant;
    private readonly ILogger<MdmPricingSkuDirectory> _logger;

    public MdmPricingSkuDirectory(HttpClient http, IConfiguration configuration, IHttpContextAccessor context, ITenantContext tenant,
        ILogger<MdmPricingSkuDirectory> logger)
    {
        _http = http;
        _context = context;
        _tenant = tenant;
        _logger = logger;
        var gateway = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
    }

    public async Task<IReadOnlyDictionary<Guid, PricingSku>?> ReadAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, PricingSku>();
        foreach (var chunk in skuIds.Where(id => id != Guid.Empty).Distinct().Chunk(BulkLimit))
        {
            var data = await SendAsync(HttpMethod.Post, BasePath + "/skus/bulk-read", new { skuIds = chunk }, cancellationToken);
            if (data is not { ValueKind: JsonValueKind.Object } body || !body.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in items.EnumerateArray())
            {
                if (Sku(row) is { } sku)
                {
                    result[sku.SkuId] = sku;
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, PricingSku>?> FindByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, PricingSku>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxCodeLookups))
        {
            foreach (var status in new[] { (string?)null, "Retired" })
            {
                var path = BasePath + "/stockable-items?pageSize=10&q=" + Uri.EscapeDataString(code) + (status is null ? string.Empty : "&status=" + status);
                var data = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
                if (data is not { ValueKind: JsonValueKind.Object } page || !page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var exact = items.EnumerateArray().Select(Sku).OfType<PricingSku>()
                    .FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    result[code] = exact;
                    break;
                }
            }
        }

        if (codes.Count > MaxCodeLookups)
        {
            _logger.LogWarning("pricing.mdm.code_lookup_capped Asked={Asked} Cap={Cap}", codes.Count, MaxCodeLookups);
        }

        return result;
    }

    public Task<IReadOnlyDictionary<string, PricingSku>?> FindByGtinsAsync(IReadOnlyCollection<string> gtins, CancellationToken cancellationToken)
    {
        if (gtins.Count > 0)
        {
            _logger.LogInformation("pricing.mdm.gtin_search_unavailable Count={Count} (product-master v1.2, BL-584 W-04)", gtins.Count);
        }

        return Task.FromResult<IReadOnlyDictionary<string, PricingSku>?>(new Dictionary<string, PricingSku>());
    }

    private static PricingSku? Sku(JsonElement row)
        => Text(row, "skuId") is { } raw && Guid.TryParse(raw, out var id) && id != Guid.Empty
            ? new PricingSku(id, Text(row, "skuLevel") ?? string.Empty, Text(row, "canonicalCode") ?? string.Empty, Text(row, "name") ?? string.Empty,
                Text(row, "lifecycleStatus") ?? string.Empty, Text(row, "gtin"))
            : null;

    private async Task<JsonElement?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (_tenant.TenantId is null)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            MdmForwarding.Forward(request, _context, _tenant);
            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("pricing.mdm.sku_read_failed Status={Status} Path={Path}", (int)response.StatusCode, path.Split('?')[0]);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = document.RootElement;
            return (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) ? data : root).Clone();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "pricing.mdm.sku_read_unavailable Path={Path}", path.Split('?')[0]);
            return null;
        }
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;
}

/// <summary>WP-VW-W6 W6-BE-b — MDM <c>GET api/legal-entities/{id}</c> (caller's token): the selling company's country, base
/// currency, name and short code. 404 → not found; any other failure → unavailable (fail closed).</summary>
public sealed class MdmPricingLegalEntityDirectory : IPricingLegalEntityDirectory
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ITenantContext _tenant;
    private readonly ILogger<MdmPricingLegalEntityDirectory> _logger;

    public MdmPricingLegalEntityDirectory(HttpClient http, IConfiguration configuration, IHttpContextAccessor context, ITenantContext tenant,
        ILogger<MdmPricingLegalEntityDirectory> logger)
    {
        _http = http;
        _context = context;
        _tenant = tenant;
        _logger = logger;
        var gateway = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
    }

    public async Task<PricingLegalEntityLookup> GetAsync(Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null || legalEntityId == Guid.Empty)
        {
            return PricingLegalEntityLookup.NotFound;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/legal-entities/" + legalEntityId.ToString("D"));
            MdmForwarding.Forward(request, _context, _tenant);
            using var response = await _http.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return PricingLegalEntityLookup.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("pricing.mdm.legal_entity_failed Status={Status}", (int)response.StatusCode);
                return PricingLegalEntityLookup.Unavailable;
            }

            var envelope = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: timeout.Token);
            var le = envelope?.Data;
            if (envelope?.IsSuccessful != true || le is null || string.IsNullOrWhiteSpace(le.CountryCode))
            {
                return PricingLegalEntityLookup.Unavailable;
            }

            return new PricingLegalEntityLookup(PricingLookupOutcome.Found, new PricingLegalEntity(legalEntityId, le.Code ?? string.Empty,
                string.IsNullOrWhiteSpace(le.DisplayName) ? le.LegalName ?? le.Code ?? string.Empty : le.DisplayName!, le.CountryCode!.Trim().ToUpperInvariant(),
                (le.BaseCurrencyCode ?? string.Empty).Trim().ToUpperInvariant()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "pricing.mdm.legal_entity_unavailable");
            return PricingLegalEntityLookup.Unavailable;
        }
    }

    private sealed record Envelope(LegalEntityBody? Data, bool IsSuccessful);

    private sealed record LegalEntityBody(string? Code, string? LegalName, string? DisplayName, string? CountryCode, string? BaseCurrencyCode);
}

internal static class MdmForwarding
{
    /// <summary>The caller's token, tenant and correlation id onto a Gateway request (never a service token).</summary>
    public static void Forward(HttpRequestMessage request, IHttpContextAccessor accessor, ITenantContext tenantContext)
    {
        var http = accessor.HttpContext;
        var authorization = http?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        var tenant = http?.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenant) && tenantContext.TenantId is { } tenantId)
        {
            tenant = tenantId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        }

        var correlation = http?.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlation))
        {
            correlation = http?.TraceIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(correlation))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        }
    }
}
