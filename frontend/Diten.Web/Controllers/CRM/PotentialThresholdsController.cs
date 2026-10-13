using System.Text.Json;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-WEB-b, §2.2 / §2.7) — <c>/CRM/PotentialThresholds</c>: the per-product Low / Medium / High boundaries of a
/// doctor's monthly patients (tenant default + product rows), the save confirm and the history. Read with
/// <c>crm.doctor-insights.read</c> | <c>.read-all</c> | <c>.thresholds.manage</c> (the CRM rule); written with
/// <c>.thresholds.manage</c>. The product picker reads the MDM Global Product selector (<c>mdm.global-products.read</c>).
/// </summary>
[Authorize]
[Route("CRM/PotentialThresholds")]
public sealed class PotentialThresholdsController : CrmGatewayProxy
{
    public const string Read = "crm.doctor-insights.read";
    public const string ReadAll = "crm.doctor-insights.read-all";
    public const string Manage = "crm.doctor-insights.thresholds.manage";
    public const string ProductRead = "mdm.global-products.read";

    private const string Base = "/api/crm/doctor-insights/thresholds";
    private const int ProductPageSize = 100;
    private const int MaxProductPages = 10;

    public PotentialThresholdsController(HttpClient httpClient, IConfiguration configuration, ILogger<PotentialThresholdsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, ReadAll, Manage);

    /// <summary>The page. Without a read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/PotentialThresholds/Index.cshtml", new PotentialThresholdsViewModel
    {
        CanRead = MayRead,
        CanManage = HasAny(Manage),
        CanReadProducts = HasAny(ProductRead),
        Me = KnowledgePathStudioReview.ActorOf(User)
    });

    [HttpGet("api/thresholds")]
    public Task<IActionResult> Thresholds(CancellationToken ct) => ProxyAsync(HttpMethod.Get, Base, null, MayRead, ct);

    [HttpGet("api/thresholds/history")]
    public Task<IActionResult> History([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Base}/history?page={Math.Max(1, page ?? 1)}&pageSize={Math.Clamp(pageSize ?? 50, 1, 200)}", null, MayRead, ct);

    /// <summary><paramref name="target"/> = a Global Product id or <c>default</c>; nothing else reaches the CRM path.</summary>
    [HttpPut("api/thresholds/{target}")]
    public async Task<IActionResult> Save(string target, CancellationToken ct)
    {
        if (!string.Equals(target, "default", StringComparison.Ordinal) && !(Guid.TryParse(target, out var id) && id != Guid.Empty))
        {
            return BadRequest(new { errors = new[] { "The target is a Global Product id or 'default'.", "doctor_insights_invalid" } });
        }

        return await ProxyAsync(HttpMethod.Put, $"{Base}/{target.ToLowerInvariant()}", await ReadBodyAsync(ct), HasAny(Manage), ct);
    }

    /// <summary>The product picker: the MDM Global Product selector read page by page. MDM unreachable ⇒ 503 (never a partial list).</summary>
    [HttpGet("api/lookups/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
    {
        if (!MayRead || !HasAny(ProductRead)) return Denied();
        var items = new List<object>();
        for (var page = 1; page <= MaxProductPages; page++)
        {
            var data = await ReadDataAsync($"/api/global-products/selector?pageSize={ProductPageSize}&pageNumber={page}", ct);
            if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { "The MDM product catalogue is not available.", "dependency_unavailable" } });
            }

            var count = 0;
            foreach (var row in rows.EnumerateArray())
            {
                count++;
                items.Add(new { id = Str(row, "id"), code = Str(row, "canonicalCode"), name = Str(row, "globalProductName") });
            }

            var total = d.TryGetProperty("totalCount", out var t) && t.TryGetInt32(out var n) ? n : 0;
            if (count == 0 || items.Count >= total) break;
        }

        return Ok(new { data = items });
    }
}
