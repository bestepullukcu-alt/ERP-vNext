using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.Mdm;

/// <summary>
/// WP-VW-W4 (W4-BE-a, §2.6) — <see cref="IMdmCompetitorClient"/> over <c>api/mdm/competitor-products</c> through the
/// Gateway with the caller's Authorization / X-Tenant-Id / X-Correlation-Id (the <c>MdmProductNameReader</c> door). The
/// answer is read defensively (the <c>{ data }</c> envelope; a list or a <c>{ items }</c> page; camelCase): BE-b2 builds
/// the MDM side in parallel. Any refusal / timeout / unreadable answer is "unavailable", never an exception.
/// </summary>
public sealed class GatewayMdmCompetitorClient : IMdmCompetitorClient
{
    private const string BasePath = "api/mdm/competitor-products";
    private const int MaxSurvivorReads = 50;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;
    private readonly ITenantContext _tenant;

    public GatewayMdmCompetitorClient(HttpClient http, IConfiguration configuration, IHttpContextAccessor context, ITenantContext tenant)
    {
        _http = http;
        _context = context;
        _tenant = tenant;
        var gateway = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
    }

    public async Task<CompetitorLookupResult> LookupAsync(Guid productId, Guid? indicationNodeId, string? market, string? q, CancellationToken ct)
    {
        var query = new List<string> { "productId=" + productId.ToString("D") };
        if (indicationNodeId is { } node) query.Add("indicationNodeId=" + node.ToString("D"));
        if (!string.IsNullOrWhiteSpace(market)) query.Add("market=" + Uri.EscapeDataString(market.Trim()));
        if (!string.IsNullOrWhiteSpace(q)) query.Add("q=" + Uri.EscapeDataString(q.Trim()));
        var data = await SendAsync(HttpMethod.Get, BasePath + "/lookup?" + string.Join('&', query), null, ct);
        if (data is not { } element) return CompetitorLookupResult.Unavailable;

        var list = element.ValueKind == JsonValueKind.Array ? element
            : element.ValueKind == JsonValueKind.Object && element.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array ? items
            : default;
        if (list.ValueKind != JsonValueKind.Array) return CompetitorLookupResult.Unavailable;

        var options = new List<CompetitorOptionDto>();
        foreach (var row in list.EnumerateArray())
        {
            if (Guid(row, "id") is not { } id || Text(row, "name") is not { } name) continue;
            var country = row.TryGetProperty("country", out var c) && c.ValueKind == JsonValueKind.Object
                ? new CompetitorCountryDto(Text(c, "marketCode"), Number(c, "boxPrice"), Text(c, "currencyCode"), Text(c, "reimbursementStatusCode"))
                : null;
            options.Add(new CompetitorOptionDto(id, name, Text(row, "company"),
                row.TryGetProperty("activeIngredients", out var a) && a.ValueKind == JsonValueKind.Array
                    ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                    : new List<string>(),
                Text(row, "formCode"), Text(row, "strength"), Text(row, "status") ?? "active", country));
        }

        return new CompetitorLookupResult(true, options);
    }

    public async Task<CompetitorProposalResult> ProposeAsync(string name, string company, Guid productId, Guid? reportId, CancellationToken ct)
    {
        var data = await SendAsync(HttpMethod.Post, BasePath + "/proposals",
            new { name, company, productId, reportId }, ct);
        return data is { ValueKind: JsonValueKind.Object } d && Guid(d, "id") is { } id
            ? new CompetitorProposalResult(true, id, null)
            : data is { ValueKind: JsonValueKind.String } s && System.Guid.TryParse(s.GetString(), out var raw)
                ? new CompetitorProposalResult(true, raw, null)
                : new CompetitorProposalResult(false, null, "mdm_unavailable");
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>?> ResolveSurvivorsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var map = new Dictionary<Guid, Guid>();
        foreach (var id in ids.Distinct().Take(MaxSurvivorReads))
        {
            var data = await SendAsync(HttpMethod.Get, BasePath + "/" + id.ToString("D"), null, ct);
            if (data is not { ValueKind: JsonValueKind.Object } d) return null;
            map[id] = Guid(d, "mergedIntoId") ?? id;
        }

        return map;
    }

    private async Task<JsonElement?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (_tenant.TenantId is null) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            Forward(request);
            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = document.RootElement;
            return (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) ? data : root).Clone();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null; // fail-open: the report never blocks on MDM
        }
    }

    private void Forward(HttpRequestMessage request)
    {
        var http = _context.HttpContext;
        var authorization = http?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        var tenant = http?.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenant) && _tenant.TenantId is { } tenantId) tenant = tenantId.ToString();
        if (!string.IsNullOrWhiteSpace(tenant)) request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);

        var correlation = http?.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlation)) correlation = http?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(correlation)) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

    private static Guid? Guid(JsonElement e, string name)
        => Text(e, name) is { } s && System.Guid.TryParse(s, out var g) && g != System.Guid.Empty ? g : null;

    private static decimal? Number(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d
            : Text(e, name) is { } s && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;
}
