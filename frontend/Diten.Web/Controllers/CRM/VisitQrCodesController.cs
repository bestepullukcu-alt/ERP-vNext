using System.Globalization;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitQrCodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-WEB-a) — the institution QR codes: the bulk page <c>/CRM/VisitQrCodes</c> (Q5), the print page
/// <c>/CRM/VisitQrCodes/Print</c> (Q2: single A4 / A5, label sheet 4 / 8) and the same-origin proxies the account card (Q1,
/// Q3, Q4) uses. Reads need <c>crm.visit-qr.read</c>; every write and every payload <c>crm.visit-qr.manage</c> (the CRM's
/// own rule — it decides again). The payload only ever leaves through the two print endpoints, as an SVG drawn HERE
/// (D-9: QRCoder, MIT), never cached (<c>no-store</c>) and never logged; the card's preview is decoration (D-8).
/// </summary>
[Authorize]
[Route("CRM/VisitQrCodes")]
public sealed class VisitQrCodesController : CrmGatewayProxy
{
    public const string Read = "crm.visit-qr.read";
    public const string Manage = "crm.visit-qr.manage";
    public const string AccountRead = "crm.account.read";

    private const string Base = "/api/crm/visit-qr";
    private const string AccountTypeSet = "account-type";
    private const string CitySet = "city";

    /// <summary>The CRM list's own page cap (VisitQrLimits.MaxPageSize).</summary>
    public const int MaxPageSize = 100;

    /// <summary>The CRM bulk cap (VisitQrLimits.MaxBulk).</summary>
    public const int MaxBulk = 200;

    public static readonly IReadOnlyList<string> SinglePapers = ["a4", "a5"];
    public static readonly IReadOnlyList<string> LabelPapers = ["labels4", "labels8"];

    public VisitQrCodesController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitQrCodesController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, Manage);

    // ---------------- pages ----------------

    /// <summary>Q5 — the bulk page. Without the read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/VisitQrCodes/Index.cshtml", new VisitQrCodesViewModel
    {
        CanRead = MayRead,
        CanManage = HasAny(Manage),
        CanReadAccounts = HasAny(AccountRead)
    });

    /// <summary>
    /// Q2 — the print page: <paramref name="accountId"/> (single, A4 / A5) or <paramref name="ids"/> (the label sheet, ≤ 200,
    /// 4 / 8 a page). It shows NO payload until "Print" is pressed — the payload (and the printed event) is fetched then.
    /// Without the manage key: the access notice only (UAS-001).
    /// </summary>
    [HttpGet("Print")]
    public async Task<IActionResult> Print([FromQuery] Guid? accountId, [FromQuery] string? ids, [FromQuery] string? mode, CancellationToken ct)
    {
        var list = ParseIds(ids);
        var single = accountId is { } a && a != Guid.Empty ? a : (Guid?)null;
        var model = new VisitQrPrintViewModel
        {
            CanPrint = HasAny(Manage),
            AccountId = single,
            AccountIds = list,
            Labels = list.Count > 0 && (single is null || string.Equals(mode, "labels", StringComparison.OrdinalIgnoreCase)),
            CanReadAccounts = HasAny(AccountRead)
        };

        // the single sticker's texts: the institution, its parent, its address (crm.account.read; else the name the print
        // answer carries — never a personal datum: no rep, no phone)
        if (model.CanPrint && single is { } id && model.CanReadAccounts && await ReadDataAsync($"/api/crm/accounts/{id}", ct) is { } account)
        {
            model.AccountName = Str(account, "accountName");
            if (Guid.TryParse(Str(account, "parentAccountId"), out var parentId) && parentId != Guid.Empty
                && await ReadDataAsync($"/api/crm/accounts/{parentId}", ct) is { } parent)
            {
                model.ParentName = Str(parent, "accountName");
            }

            var cityCode = Str(account, "cityRef");
            var districtCode = Str(account, "districtRef");
            // G-3: the city in words — the set's label, else the readable name of TR-NN-NAME (never the raw code)
            var city = string.IsNullOrWhiteSpace(cityCode) ? null : CrmCityLabels.Resolve(cityCode, await ReadReferenceLabelsAsync(CitySet, ct));
            var district = string.IsNullOrWhiteSpace(districtCode) ? null : (await ReadReferenceLabelsAsync("district", ct)).GetValueOrDefault(districtCode, districtCode);
            model.Address = AddressOf(Str(account, "addressLine"), district, city);
        }

        return View("~/Views/CRM/VisitQrCodes/Print.cshtml", model);
    }

    /// <summary>Static, so MVC never treats it as an action: "line, district / city" with the empty parts left out.</summary>
    public static string? AddressOf(string? line, string? district, string? city)
    {
        var place = string.Join(" / ", new[] { district, city }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        var text = string.Join(", ", new[] { line?.Trim(), place }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return text.Length == 0 ? null : text;
    }

    // ---------------- reads ----------------

    /// <summary>
    /// W3E-FIX (U-1) — Q5 is a Golden Compact server-mode list: the shared factory asks with the list contract (start ·
    /// length · search · orderBy · orderDir · draw · each applied filter by its key) and reads { items, total, filteredTotal }.
    /// The CRM list keeps ITS contract (page / pageSize ≤ 100, one value per filter, ordered by institution name); this
    /// proxy translates between the two and nothing else. <c>total</c> is the tenant's whole list (a second, one-row read
    /// only when a filter or a search is applied), <c>filteredTotal</c> what matched. Contract refusals: a sort key (the CRM
    /// orders by name only — no column is sortable, so the factory never sends one) or a page size outside 1–100 → 400.
    /// </summary>
    [HttpGet("api/accounts")]
    public async Task<IActionResult> List(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? status, [FromQuery] string? accountType, [FromQuery] string? city, [FromQuery] string? territoryId, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (ListRefusal(length, orderBy, orderDir) is { } refusal) return BadRequest(new { errors = new[] { refusal, "visit_qr_request_invalid" } });
        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        using var response = await SendGatewayAsync(HttpMethod.Get, ListPath(status, accountType, city, territoryId, search, page, size), null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        if (await PageOf(response, ct) is not { } matched) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });

        var total = matched.Total;
        var narrowed = new[] { status, accountType, city, territoryId, search }.Any(x => !string.IsNullOrWhiteSpace(x));
        if (narrowed)
        {
            using var all = await SendGatewayAsync(HttpMethod.Get, ListPath(null, null, null, null, null, 1, 1), null, ct);
            total = all is { IsSuccessStatusCode: true } && await PageOf(all, ct) is { } whole ? whole.Total : matched.Total;
        }

        return Ok(new { data = new { items = matched.Items, total, filteredTotal = matched.Total } });
    }

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? ListRefusal(int? length, string? orderBy, string? orderDir)
    {
        if (length is { } n && (n < 1 || n > MaxPageSize)) return $"length must be 1–{MaxPageSize}.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list is ordered by institution name; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        return null;
    }

    private sealed record ListPage(JsonElement Items, long Total);

    private static async Task<ListPage?> PageOf(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;
            var total = data.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var n) ? n : items.GetArrayLength();
            return new ListPage(items.Clone(), total);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------------- the export (BL-452: the file is the screen) ----------------

    /// <summary>The export's columns: the list's own data keys, in the screen's words (the request culture).</summary>
    public static readonly IReadOnlyList<string> ExportKeys = ["accountName", "accountType", "cityRef", "status", "version", "lastScannedAt", "recentScanCount"];

    /// <summary>
    /// W3E-FIX (U-1) — <c>GET api/accounts/export?format=csv|xlsx&amp;columns=…</c> + the list's own search and filters: EVERY
    /// matching institution (the CRM list read page by page, 100 at a time — the CRM has no export of its own and this
    /// package changes nothing there), the visible columns in screen order, the type / city / QR status in the reader's
    /// language. More than 50 000 → 413 EXPORT_TOO_LARGE before a single row is written; an unknown column → 400. The QR
    /// payload is not part of a list row, so it can never reach a file.
    /// </summary>
    [HttpGet("api/accounts/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? status, [FromQuery] string? accountType, [FromQuery] string? city, [FromQuery] string? territoryId, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        var set = ExportColumns(culture);
        if (!set.TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        if (ListRefusal(null, orderBy, orderDir) is { } refusal) return ExportRefusal(400, refusal, "visit_qr_request_invalid");

        var rows = new List<JsonElement>();
        for (var page = 1; ; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, ListPath(status, accountType, city, territoryId, search, page, MaxPageSize), null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            if (await PageOf(response, ct) is not { } chunk) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
            if (chunk.Total > ListExportContract.MaxRows)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                    $"{chunk.Total} institutions match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter.", ListExportContract.TooLargeCode);
            }

            rows.AddRange(chunk.Items.EnumerateArray());
            if (chunk.Items.GetArrayLength() < MaxPageSize || rows.Count >= chunk.Total) break;
        }

        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var types = await ReadReferenceLabelsAsync(AccountTypeSet, ct);
            var cities = await ReadReferenceLabelsAsync(CitySet, ct);
            var lines = rows.Select(r => ExportRowOf(r, types, cities)).ToList();
            var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text<VisitQrCodesIndex>("VisitQrCodesTitle", culture));
            return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("institution-qr-codes", DateTimeOffset.UtcNow, fileFormat));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>One institution as the export writes it (the labels already in the request culture).</summary>
    public sealed record QrExportRow(string Name, string? Parent, string? Type, string? City, string Status, int? Version, DateTimeOffset? LastScannedAt, int? Recent);

    private static QrExportRow ExportRowOf(JsonElement row, IReadOnlyDictionary<string, string> types, IReadOnlyDictionary<string, string> cities)
    {
        var type = Str(row, "accountType");
        var status = Str(row, "status") is { Length: > 0 } s ? s : "none";
        return new QrExportRow(
            Str(row, "accountName") ?? string.Empty,
            Str(row, "parentAccountName"),
            string.IsNullOrWhiteSpace(type) ? null : types.GetValueOrDefault(type, type),
            CrmCityLabels.Resolve(Str(row, "cityRef"), cities),
            status,
            Int(row, "version"),
            row.TryGetProperty("lastScannedAt", out var l) && l.ValueKind == JsonValueKind.String && l.TryGetDateTimeOffset(out var at) ? at : null,
            Int(row, "recentScanCount"));
    }

    /// <summary>A number field of a CRM row; null when absent or null (a code-less institution has no version).</summary>
    private static int? Int(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private ListExportColumnSet<QrExportRow> ExportColumns(CultureInfo culture) => new(
    [
        new("accountName", c => Text<VisitQrCodesIndex>("ColInstitution", c), (r, _) => r.Parent is { Length: > 0 } p ? r.Name + " (↳ " + p + ")" : r.Name),
        new("accountType", c => Text<VisitQrCodesIndex>("ColType", c), (r, _) => r.Type),
        new("cityRef", c => Text<VisitQrCodesIndex>("ColCity", c), (r, _) => r.City),
        new("status", c => Text<VisitQrCodesIndex>("ColStatus", c), (r, c) => Text<VisitQrIndex>("Chip_" + r.Status, c)),
        new("version", c => Text<VisitQrCodesIndex>("ColVersion", c), (r, _) => r.Version?.ToString(CultureInfo.InvariantCulture)),
        new("lastScannedAt", c => Text<VisitQrCodesIndex>("ColLastScan", c), (r, c) => r.LastScannedAt?.ToString("g", c)),
        new("recentScanCount", c => Text<VisitQrCodesIndex>("ColRecent", c), (r, _) => r.Recent?.ToString(CultureInfo.InvariantCulture))
    ]);

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text<TMarker>(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<TMarker>)) as IStringLocalizer<TMarker>;
        if (localizer is null) return key;
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var text = localizer[key];
            return text.ResourceNotFound ? key : text.Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private ObjectResult ExportRefusal(int statusCode, string message, string code) => StatusCode(statusCode, new
    {
        isSuccessful = false,
        statusCode,
        errors = new[] { message },
        errorCodes = new[] { new { code } }
    });

    /// <summary>Static, so MVC never treats it as an action: the CRM list path — every filter escaped, the page clamped
    /// (1 … ; 1 … 100), empty filters left out.</summary>
    public static string ListPath(string? status, string? accountType, string? city, string? territoryId, string? search, int? page, int? pageSize)
    {
        var q = new List<string>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) q.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        Add("status", status);
        Add("accountType", accountType);
        Add("city", city);
        Add("territoryId", territoryId);
        Add("search", search);
        q.Add("page=" + Math.Max(1, page ?? 1));
        q.Add("pageSize=" + Math.Clamp(pageSize ?? 25, 1, MaxPageSize));
        return Base + "/accounts?" + string.Join("&", q);
    }

    [HttpGet("api/accounts/{accountId:guid}")]
    public Task<IActionResult> Get(Guid accountId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}", null, MayRead, ct);

    [HttpGet("api/accounts/{accountId:guid}/history")]
    public Task<IActionResult> History(Guid accountId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}/history", null, MayRead, ct);

    /// <summary>The list's filter options in the user's language: institution types and cities (MOD-0048 sets). A set
    /// that cannot be read is an empty list (the filter then offers only "all").</summary>
    [HttpGet("api/lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var types = await ReadReferenceLabelsAsync(AccountTypeSet, ct);
        var cities = await ReadReferenceLabelsAsync(CitySet, ct);
        static object Options(IReadOnlyDictionary<string, string> map)
            => map.OrderBy(x => x.Value, StringComparer.CurrentCultureIgnoreCase).Select(x => new { value = x.Key, text = x.Value }).ToList();
        return Ok(new { data = new { accountTypes = Options(types), cities = Options(cities) } });
    }

    // ---------------- writes ----------------

    /// <summary>Q1a / Q1c "Generate" and Q3 "Regenerate" (the CRM tells them apart by the active code; expectedVersion).</summary>
    [HttpPost("api/accounts/{accountId:guid}/issue")]
    public async Task<IActionResult> Issue(Guid accountId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/accounts/{accountId}/issue", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    /// <summary>Q4 — revoke (reason required, ≤ 500 — the CRM checks again).</summary>
    [HttpPost("api/accounts/{accountId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid accountId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/accounts/{accountId}/revoke", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    /// <summary>Q5 "Generate for the selected (those without a code)" — the list's bulk endpoint (W3E-FIX U-1: the shared bulk
    /// bar's action; the CRM <c>bulk-issue</c> skips the institutions that already have an active code, ≤ 200).</summary>
    [HttpPost("api/accounts/bulk")]
    public async Task<IActionResult> BulkIssue(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/bulk-issue", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    // ---------------- the print payload (manage; no-store; never logged) ----------------

    /// <summary>
    /// Q2 single — the active code drawn as an SVG for <paramref name="paper"/> (a4 | a5) × <paramref name="copies"/> (1–50):
    /// the CRM writes the <c>printed</c> event with both. The answer carries the SVG and the sticker's texts, NOT the payload.
    /// </summary>
    [HttpGet("api/accounts/{accountId:guid}/print")]
    public async Task<IActionResult> PrintOne(Guid accountId, [FromQuery] string? paper, [FromQuery] int? copies, CancellationToken ct)
    {
        NoStore();
        if (!HasAny(Manage)) return Denied();
        var p = (paper ?? string.Empty).Trim().ToLowerInvariant();
        if (!SinglePapers.Contains(p) || copies is not (>= 1 and <= 50))
        {
            return BadRequest(new { errors = new[] { "paper must be a4 or a5 and copies 1–50.", "visit_qr_request_invalid" } });
        }

        var response = await SendGatewayAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}/payload?paper={p}&copies={copies}", null, ct);
        return await ToPrintResultAsync(response, single: true, ct);
    }

    /// <summary>Q2 labels — the ACTIVE codes of the selected institutions (≤ 200) as SVGs, labels4 | labels8; one printed
    /// event each (CRM). An institution without an active code is simply not in the answer.</summary>
    [HttpPost("api/bulk-print")]
    public async Task<IActionResult> PrintMany(CancellationToken ct)
    {
        NoStore();
        if (!HasAny(Manage)) return Denied();
        var body = await ReadBodyAsync(ct);
        if (body is null || ContainsTenantId(body)) return BadRequest(new { errors = new[] { "The request body is invalid.", "visit_qr_request_invalid" } });
        BulkPrintInput? input;
        try
        {
            input = JsonSerializer.Deserialize<BulkPrintInput>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            input = null;
        }

        var paper = (input?.Paper ?? string.Empty).Trim().ToLowerInvariant();
        if (input?.AccountIds is not { Count: > 0 and <= MaxBulk } || !LabelPapers.Contains(paper) || input.Copies is not (>= 1 and <= 50))
        {
            return BadRequest(new { errors = new[] { "1–200 institutions, paper labels4 or labels8 and copies 1–50.", "visit_qr_request_invalid" } });
        }

        var forward = JsonSerializer.Serialize(new { accountIds = input.AccountIds, paper, copies = input.Copies });
        var response = await SendGatewayAsync(HttpMethod.Post, $"{Base}/bulk-payload", new StringContent(forward, Encoding.UTF8, "application/json"), ct);
        return await ToPrintResultAsync(response, single: false, ct);
    }

    public sealed record BulkPrintInput(IReadOnlyList<Guid>? AccountIds, string? Paper, int? Copies);

    /// <summary>The CRM payload answer → the same shape with <c>svg</c> instead of <c>payload</c>; any failure as it came.</summary>
    private async Task<IActionResult> ToPrintResultAsync(HttpResponseMessage? response, bool single, CancellationToken ct)
    {
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
        if (single)
        {
            return data.ValueKind == JsonValueKind.Object ? Ok(new { data = Sticker(data) }) : StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        }

        return data.ValueKind == JsonValueKind.Array
            ? Ok(new { data = data.EnumerateArray().Select(Sticker).ToList() })
            : StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
    }

    /// <summary>One payload row → the sticker (the payload itself never leaves this method except as the drawing).</summary>
    private static object Sticker(JsonElement row)
    {
        var payload = Str(row, "payload") ?? string.Empty;
        return new
        {
            accountId = Str(row, "accountId"),
            accountName = Str(row, "accountName"),
            accountCode = Str(row, "accountCode"),
            version = row.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : 0,
            svg = VisitQrSvg.Render(payload)
        };
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    /// <summary>Static, so MVC never treats it as an action: "a,b,c" → distinct non-empty ids, at most 200.</summary>
    public static IReadOnlyList<Guid> ParseIds(string? ids)
        => (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().Take(MaxBulk).ToList();
}
