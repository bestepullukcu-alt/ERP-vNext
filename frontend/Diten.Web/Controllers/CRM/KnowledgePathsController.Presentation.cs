using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-c) — the knowledge path studio's "Sunum notları" tab: the expected-objection picker. The step save
/// itself is the existing step PUT proxy (the path's manage key; the body carries the COMPLETE <c>presentation</c> object).
/// </summary>
public sealed partial class KnowledgePathsController
{
    internal const string ObjectionReadPermission = ObjectionResponsesController.Read;
    internal const string ObjectionManagePermission = ObjectionResponsesController.Manage;

    /// <summary>
    /// The objection answers a step of <paramref name="pathId"/> may link: this path's product (the chain derives it), APPROVED
    /// (K-A4 — CRM refuses anything else with <c>path_step_objection_invalid</c>), at most 100 offered. <paramref name="ids"/>
    /// (comma-separated, ≤ 10) are the step's already-linked answers: each one outside that set is read by id, so the tab can
    /// name a linked answer that was retired or is not approved any more (it is dropped on the next save). Category labels
    /// come from the BRD set in the page language. Without the library's read key, or for a path with no product, the picker
    /// is disabled with a reason — never an invented list.
    /// </summary>
    [HttpGet("api/lookups/path-objections")]
    public async Task<IActionResult> PathObjectionLookup([FromQuery] Guid pathId, [FromQuery] string? ids, CancellationToken ct)
    {
        if (RequireJson(ReadPermission, ReadFallback) is { } denied) return denied;
        if (!HasAnyPermission(ObjectionReadPermission))
        {
            return Json(new { disabled = true, reason = "ObjectionReadMissing", productId = (string?)null, options = Array.Empty<object>() });
        }

        var detail = await ReadDataAsync($"/api/crm/knowledge/paths/{pathId}", ct);
        if (detail is not { ValueKind: JsonValueKind.Object } p) return NotFound(new { errors = new[] { "Knowledge path not found." } });
        var productId = p.TryGetProperty("derivedContext", out var dc) && dc.ValueKind == JsonValueKind.Object ? Str(dc, "productId") : null;

        var rows = new List<JsonElement>();
        if (productId is not null)
        {
            var approved = await ReadItemsAsync(
                $"/api/crm/objection-responses?productId={Uri.EscapeDataString(productId)}&status=approved&page=1&pageSize=100", ct);
            if (approved is null) return Unavailable();
            rows.AddRange(approved);
        }

        var linked = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => Guid.TryParse(x, out _)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        foreach (var id in linked.Where(id => !rows.Any(r => string.Equals(Str(r, "id"), id, StringComparison.OrdinalIgnoreCase))))
        {
            if (await ReadDataAsync($"/api/crm/objection-responses/{id}", ct) is { ValueKind: JsonValueKind.Object } one) rows.Add(one);
        }

        var categories = await ObjectionResponsesController.ReadCategoryValuesAsync(
            new Diten.Web.Services.CrmReferenceSetReader(_httpClient, _gatewayUrl, _logger),
            Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
        var labels = (categories ?? [])
            .Select(c => JsonSerializer.SerializeToElement(c))
            .ToDictionary(c => Str(c, "code") ?? string.Empty, c => Str(c, "label"), StringComparer.Ordinal);

        return Json(new
        {
            disabled = productId is null,
            reason = productId is null ? "ObjectionNoProduct" : null,
            productId,
            options = rows.Select(r => new
            {
                id = Str(r, "id"),
                productId = Str(r, "productId"),
                status = Str(r, "status"),
                categoryCode = Str(r, "categoryCode"),
                categoryLabel = Str(r, "categoryCode") is { } code ? labels.GetValueOrDefault(code) ?? code : null,
                question = Str(r, "question"),
                answer = Str(r, "answer"),
                claimCount = r.TryGetProperty("claimIds", out var cl) && cl.ValueKind == JsonValueKind.Array ? cl.GetArrayLength() : 0
            }).ToList()
        });
    }
}
