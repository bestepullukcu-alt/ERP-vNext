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
    public const string CompetitorRead = "mdm.competitor-products.read";
    public const string AccountContactRead = "crm.account-contact.read";
    public const string PlannedVisitRead = "crm.planned-visit.read";
    public const string AccountRead = "crm.account.read";
    public const string MedicalSpecialtySet = "medical-specialty";
    public const string CitySet = "city";

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
        CanReadCompetitors = HasAny(CompetitorRead),
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

    /// <summary>
    /// WP-VW-W4 (W4-FIX-d, G-9) — the MDM competitors of ONE product (a GSKU the rep added whose product the pre-fill did not
    /// list, e.g. an unplanned pharmacy visit): the same MDM lookup the doctor wizard reads (<c>mdm.competitor-products.read</c>).
    /// </summary>
    [HttpGet("api/lookups/competitors")]
    public Task<IActionResult> Competitors([FromQuery] Guid productId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/mdm/competitor-products/lookup?productId={productId:D}", null,
            productId != Guid.Empty && HasAny(Read) && HasAny(CompetitorRead), ct);

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

        var found = rows.Value.EnumerateArray().Select(r => new
        {
            id = Str(r, "id"),
            name = Str(r, "displayName") ?? string.Join(' ', new[] { Str(r, "firstName"), Str(r, "lastName") }.Where(x => !string.IsNullOrWhiteSpace(x)))
        }).Where(x => x.id is not null && Guid.TryParse(x.id, out _)).Take(10).ToList();
        if (found.Count == 0) return Ok(new { data = Array.Empty<object>() });

        // W4-FIX-c (G-10) — two doctors with the same name must be told apart: the CRM search answers only id + name, so each
        // hit is completed from the EXISTING reads — the contact (crm.contact.read: specialty code, city code) and, with
        // crm.account-contact.read, its linked accounts (the primary institution). Codes become labels through the BRD sets
        // (medical-specialty, city); a read that fails leaves its field empty (never a raw code shown as a label).
        var withAccounts = HasAny(AccountContactRead);
        var specialties = ReadReferenceLabelsAsync(MedicalSpecialtySet, ct);
        var cities = ReadReferenceLabelsAsync(CitySet, ct);
        var details = found.Select(x => ReadDataAsync($"/api/crm/contacts/{x.id}", ct)).ToArray();
        var accounts = found.Select(x => withAccounts ? ReadDataAsync($"/api/crm/contacts/{x.id}/accounts", ct) : Task.FromResult<JsonElement?>(null)).ToArray();
        await Task.WhenAll(details.Cast<Task>().Concat(accounts).Append(specialties).Append(cities));

        return Ok(new
        {
            data = found.Select((x, i) =>
            {
                var detail = details[i].Result is { ValueKind: JsonValueKind.Object } d ? d : (JsonElement?)null;
                var specialtyCode = detail is { } dd ? Str(dd, "specialty") : null;
                var cityCode = detail is { } dc ? Str(dc, "cityRef") : null;
                return new
                {
                    x.id,
                    x.name,
                    specialtyCode,
                    specialty = specialtyCode is not null && specialties.Result.TryGetValue(specialtyCode, out var s) ? s : null,
                    city = CityLabel(cities.Result, cityCode),
                    institution = PrimaryInstitution(accounts[i].Result)
                };
            }).ToList()
        });
    }

    /// <summary>
    /// W4-FIX-c (G-7) — the report header's pharmacy: its name from the planned visit (crm.planned-visit.read; the CRM reads
    /// <c>targetDisplayName</c> at read time) and, with crm.account.read, its city (the account's <c>cityRef</c> → the city
    /// BRD label). No new CRM endpoint: two existing reads. Without the plan read → 403 and the header keeps only the day.
    /// </summary>
    [HttpGet("api/{plannedVisitId:guid}/target")]
    public async Task<IActionResult> Target(Guid plannedVisitId, CancellationToken ct)
    {
        if (!HasAny(Read) || !HasAny(PlannedVisitRead)) return Denied();
        if (await ReadDataAsync($"/api/crm/planned-visits/{plannedVisitId:D}", ct) is not { ValueKind: JsonValueKind.Object } plan)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { "The planned visit is not available.", "dependency_unavailable" } });
        }

        var name = Str(plan, "targetDisplayName") ?? Str(plan, "accountDisplayName");
        var accountId = Str(plan, "accountId") ?? (string.Equals(Str(plan, "targetType"), "pharmacy", StringComparison.OrdinalIgnoreCase) ? Str(plan, "targetId") : null);
        string? city = null;
        if (HasAny(AccountRead) && Guid.TryParse(accountId, out var account)
            && await ReadDataAsync($"/api/crm/accounts/{account:D}", ct) is { ValueKind: JsonValueKind.Object } detail)
        {
            name ??= Str(detail, "accountName");
            if (Str(detail, "cityRef") is { Length: > 0 } cityCode)
            {
                city = CityLabel(await ReadReferenceLabelsAsync(CitySet, ct), cityCode);
            }
        }

        return Ok(new { data = new { name, city } });
    }

    /// <summary>W4-FIX-c (G-6) — the BRD sets the pre-fill does not carry (the competitor card's reimbursement status): only
    /// <see cref="ReadableSets"/>, read through the shared CrmReferenceSetReader; the page key is enough.</summary>
    public static readonly string[] ReadableSets = ["mdm-reimbursement-status"];

    [HttpGet("api/reference-data/{setCode}/values")]
    public async Task<IActionResult> ReferenceValues(string setCode, CancellationToken ct)
    {
        if (!HasAny(Read)) return Denied();
        if (!ReadableSets.Contains(setCode, StringComparer.Ordinal)) return NotFound(new { errors = new[] { "Unknown set.", "unknown_set" } });
        return await ToProxyResultAsync(await ReadReferenceSetAsync(setCode, ct), ct);
    }

    /// <summary>
    /// The city label of a stored city reference — the Visit Planning page's rule (details.js cityLabel): the masters carry
    /// "TR-01-ADANA" while the city set's codes are "adana", so the full code, then "TR-01", then "01", then the last part are
    /// looked up; with no label the last part is written the Turkish way ("ADANA" → "Adana"), never the raw reference.
    /// </summary>
    public static string? CityLabel(IReadOnlyDictionary<string, string> labels, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var parts = code.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        var keys = new List<string> { code.Trim() };
        if (parts.Length > 2) keys.Add(string.Join('-', parts.Take(2)));
        if (parts.Length > 1) keys.Add(parts[1]);
        if (parts.Length > 0) keys.Add(parts[^1]);
        foreach (var key in keys)
        {
            if (labels.TryGetValue(key, out var label) && !string.IsNullOrWhiteSpace(label)) return label;
        }

        var last = parts.Length > 0 ? parts[^1] : code.Trim();
        if (last.All(char.IsDigit)) return null;
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var lower = last.ToLower(turkish);
        return char.ToUpper(lower[0], turkish) + lower[1..];
    }

    /// <summary>The primary active institution of a contact (else its first active one), or null.</summary>
    private static string? PrimaryInstitution(JsonElement? links)
    {
        if (links is not { ValueKind: JsonValueKind.Array } list) return null;
        var active = list.EnumerateArray().Where(l => !string.Equals(Str(l, "status"), "inactive", StringComparison.OrdinalIgnoreCase)
                                                       && !string.Equals(Str(l, "status"), "ended", StringComparison.OrdinalIgnoreCase)).ToList();
        var primary = active.FirstOrDefault(l => l.TryGetProperty("isPrimary", out var p) && p.ValueKind == JsonValueKind.True);
        return primary.ValueKind == JsonValueKind.Object ? Str(primary, "accountName") : active.Select(l => Str(l, "accountName")).FirstOrDefault(n => n is not null);
    }

    private IReadOnlyDictionary<string, string> IfMatch()
        => new Dictionary<string, string> { ["If-Match"] = Request.Headers.IfMatch.ToString() };
}
