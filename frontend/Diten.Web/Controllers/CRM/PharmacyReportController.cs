using System.Text.Json;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-WEB-b, §2.5) — <c>/CRM/PharmacyReport/{plannedVisitId}</c>: the pharmacy visit report (mockup 03-rapor
/// pharmacy steps: General → Stock and shelf → Order and sales → Competitor and recommendation → Safety → Summary). The CRM
/// report v2 endpoints decide (kind, completeness, codes, the 60-minute rule); every call goes through the Gateway with the
/// caller's session. Keys: the CRM endpoint's — read <c>crm.visit-report.read</c>; draft / submit / shelf photo
/// <c>crm.visit-report.record</c> + <c>crm.planned-visit.manage</c>; amend <c>crm.visit-report.amend</c>; the GSKU list
/// <c>mdm.gskus.read</c>; the linked doctors <c>crm.contact.read</c>.
/// </summary>
[Authorize]
[Route("CRM/PharmacyReport")]
public sealed class PharmacyReportController : CrmGatewayProxy
{
    public const string Read = "crm.visit-report.read";
    public const string Record = "crm.visit-report.record";
    public const string Amend = "crm.visit-report.amend";
    public const string PlannedVisitManage = "crm.planned-visit.manage";
    public const string GskuRead = "mdm.gskus.read";
    public const string ContactRead = "crm.contact.read";

    /// <summary>11 MB: a 10 MB photo + the multipart envelope (the CRM's own ceiling).</summary>
    public const long MaxPhotoRequestBytes = 11L * 1024 * 1024;

    private const string Report = "/api/crm/visit-report";

    public PharmacyReportController(HttpClient httpClient, IConfiguration configuration, ILogger<PharmacyReportController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayWrite => HasAll(Record, PlannedVisitManage);

    /// <summary>The page. Without <c>crm.visit-report.read</c> the view draws only the access notice (UAS-001).</summary>
    [HttpGet("{plannedVisitId:guid}")]
    public IActionResult Index(Guid plannedVisitId) => View("~/Views/CRM/PharmacyReport/Index.cshtml", new PharmacyReportViewModel
    {
        PlannedVisitId = plannedVisitId,
        CanRead = HasAny(Read),
        CanRecord = MayWrite,
        CanAmend = HasAny(Amend),
        CanReadGskus = HasAny(GskuRead),
        CanReadContacts = HasAny(ContactRead),
        Me = KnowledgePathStudioReview.ActorOf(User)
    });

    [HttpGet("api/{plannedVisitId:guid}/prefill")]
    public Task<IActionResult> Prefill(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Report}/v2/prefill?plannedVisitId={plannedVisitId:D}", null, HasAny(Read), ct);

    /// <summary>The draft (the version travels as <c>If-Match</c>).</summary>
    [HttpPut("api/draft")]
    public async Task<IActionResult> Draft(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Put, $"{Report}/v2/draft", await ReadBodyAsync(ct), MayWrite, ct, IfMatch());

    [HttpPost("api/submit")]
    public async Task<IActionResult> Submit(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Report}/v2/submit", await ReadBodyAsync(ct), MayWrite, ct, IfMatch());

    /// <summary>The reasoned correction after the 60 minutes (body <c>{ reason, v2 }</c>).</summary>
    [HttpPost("api/{visitReportId:guid}/amend")]
    public async Task<IActionResult> AmendReport(Guid visitReportId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Report}/{visitReportId:D}/amend", await ReadBodyAsync(ct), HasAny(Amend), ct);

    /// <summary>The shelf photo: multipart (<c>file</c>) streamed to the CRM as it came; answers <c>{ photoEvidenceId }</c>.</summary>
    [HttpPost("api/{plannedVisitId:guid}/shelf-photo")]
    [Diten.Web.Filters.DisableFormValueModelBinding] // keep the multipart body unread for the copy (W3 live E4)
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    public Task<IActionResult> ShelfPhoto(Guid plannedVisitId, CancellationToken ct)
        => ProxyMultipartAsync($"{Report}/v2/{plannedVisitId:D}/shelf-photo", MayWrite, ct);

    [HttpGet("api/{plannedVisitId:guid}/shelf-photo")]
    public Task<IActionResult> ShelfPhotoImage(Guid plannedVisitId, CancellationToken ct)
        => ProxyBytesAsync($"{Report}/v2/{plannedVisitId:D}/shelf-photo", HasAny(Read), ct);

    /// <summary>The GSKUs (MDM list, search): <c>{ id, code, productId, productName, pack }</c>.</summary>
    [HttpGet("api/lookups/gskus")]
    public async Task<IActionResult> Gskus([FromQuery] string? q, CancellationToken ct)
    {
        if (!HasAny(Read) || !HasAny(GskuRead)) return Denied();
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&search=" + Uri.EscapeDataString(q.Trim());
        var data = await ReadDataAsync($"/api/gskus?pageNumber=1&pageSize=100{search}", ct);
        if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { "The MDM SKU list is not available.", "dependency_unavailable" } });
        }

        return Ok(new
        {
            data = rows.EnumerateArray().Select(r => new
            {
                id = Str(r, "id"),
                code = Str(r, "canonicalCode"),
                productId = Str(r, "globalProductId"),
                productName = Str(r, "globalProductName"),
                pack = (r.TryGetProperty("packQuantity", out var q2) && q2.ValueKind == JsonValueKind.Number ? q2.GetDecimal().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " " : string.Empty)
                       + (Str(r, "packUomCode") ?? string.Empty)
            }).ToList()
        });
    }

    /// <summary>The doctors a pharmacy recommends to (CRM contacts, search ≥ 2 characters): <c>{ id, name }</c>.</summary>
    [HttpGet("api/lookups/contacts")]
    public async Task<IActionResult> Contacts([FromQuery] string? q, CancellationToken ct)
    {
        if (!HasAny(Read) || !HasAny(ContactRead)) return Denied();
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(new { data = Array.Empty<object>() });
        var data = await ReadDataAsync($"/api/crm/contacts/search?search={Uri.EscapeDataString(q.Trim())}&limit=10", ct);
        var rows = data is { ValueKind: JsonValueKind.Array } a ? a
            : data is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array ? i
            : (JsonElement?)null;
        if (rows is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { "The contact search is not available.", "dependency_unavailable" } });
        }

        return Ok(new
        {
            data = rows.Value.EnumerateArray().Select(r => new
            {
                id = Str(r, "id"),
                name = Str(r, "displayName") ?? string.Join(' ', new[] { Str(r, "firstName"), Str(r, "lastName") }.Where(x => !string.IsNullOrWhiteSpace(x)))
            }).Where(x => x.id is not null).ToList()
        });
    }

    private IReadOnlyDictionary<string, string> IfMatch()
        => new Dictionary<string, string> { ["If-Match"] = Request.Headers.IfMatch.ToString() };
}
