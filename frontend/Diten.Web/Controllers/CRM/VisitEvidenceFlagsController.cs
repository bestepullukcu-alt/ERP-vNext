using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitEvidenceFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-WEB-b, M1–M6) — <c>/CRM/VisitEvidenceFlags</c>: the manager's visit evidence flags (manifest page
/// <c>CRM_VISIT_EVIDENCE_FLAGS</c>): the summary card, the visits list with its filters, the quick view and the rep
/// summary. Read only (K-W3E-11). Every call is a same-origin proxy to the CRM endpoints of W3E-BE-b / BE-c with the
/// caller's session; the CRM decides the scope (<c>crm.visit-evidence-flags.read</c> = the team, <c>.read-all</c> = the
/// tenant; outside the scope 404). The photo thumbnail is read through the EXISTING evidence path with its OWN keys:
/// <c>crm.visit-execution.read</c> + <c>crm.planned-visit.read-all</c> (another rep's visit) + the Platform
/// <c>platform.document-repository.visit-evidence.read-all</c>; without them the quick view says "photo present" only.
/// <para>
/// W3E-FLAGS-STD (K-STD) — the visits list is a Golden Compact server-mode list: the shared factory asks with the list
/// contract (start · length · search · orderBy · orderDir · draw · each applied filter by its key: period, from, to,
/// resourceId, flags (repeated), accountId, scope) and reads { items, total, filteredTotal }. The CRM keeps ITS contract
/// (from / to ≤ 92 days, flags as one csv, onlyFlagged, page / pageSize ≤ 100, newest start first); this proxy translates
/// between the two and nothing else (<see cref="ResolveRange"/>, <see cref="ListPath"/>). The CRM contract is untouched.
/// </para>
/// </summary>
[Authorize]
[Route("CRM/VisitEvidenceFlags")]
public sealed class VisitEvidenceFlagsController : CrmGatewayProxy
{
    public const string Read = "crm.visit-evidence-flags.read";
    public const string ReadAll = "crm.visit-evidence-flags.read-all";
    public const string ExecutionRead = "crm.visit-execution.read";
    public const string PlannedVisitReadAll = "crm.planned-visit.read-all";
    public const string EvidenceReadAll = "platform.document-repository.visit-evidence.read-all";

    /// <summary>The CRM list's own page cap (the evidence flags list pageSize ≤ 100).</summary>
    public const int MaxPageSize = 100;

    /// <summary>The CRM's window cap: a range is at most 92 days, both ends included.</summary>
    public const int MaxRangeDays = 92;

    /// <summary>The periods the filter offers; "" (the baseline) is the last 7 days.</summary>
    public static readonly IReadOnlyList<string> Periods = ["", "last7", "last30", "month", "custom"];

    /// <summary>The "only flagged" switch travels as <c>scope</c>: "" (the baseline) = only flagged, "all" = every visit.</summary>
    public const string ScopeAll = "all";

    private const string Flags = "/api/crm/visit-evidence-flags";
    private const string RangeInvalid = "visit_evidence_flags_range_invalid";
    private const string RequestInvalid = "visit_evidence_flags_filter_invalid";

    public VisitEvidenceFlagsController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitEvidenceFlagsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, ReadAll);

    /// <summary>The photo thumbnail's keys (the existing evidence read path: another rep's visit needs read-all + the
    /// Platform tenant-wide evidence read).</summary>
    private bool MaySeePhotos => HasAll(ExecutionRead, PlannedVisitReadAll, EvidenceReadAll);

    /// <summary>"Today" in UTC days — the same day the page's period labels use (a TimeProvider from DI when there is one).</summary>
    private DateOnly Today()
    {
        var clock = HttpContext?.RequestServices?.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>The page. Without a flags read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/VisitEvidenceFlags/Index.cshtml", new VisitEvidenceFlagsViewModel
    {
        CanRead = MayRead,
        CanReadAll = HasAny(ReadAll),
        CanSeePhotos = MaySeePhotos
    });

    // ---------------- the visits list (server mode) ----------------

    /// <summary>
    /// The visits list in the list contract. <c>start / length</c> → the CRM's <c>page / pageSize</c>; the period (or the
    /// custom <c>from / to</c>) → the CRM window; <c>flags</c> (repeated) → one csv; <c>scope</c> "" → onlyFlagged=true,
    /// "all" → false. <c>total</c> is every visit started in the window (a second, one-row read only when the list is
    /// narrowed), <c>filteredTotal</c> what matched. Refusals before any CRM call: a sort key (the CRM orders by the
    /// start, newest first — no column sorts), a page size outside 1–100, a window the CRM would refuse (&gt; 92 days, from
    /// after to, a missing custom end) → 400 with the CRM's own code.
    /// </summary>
    [HttpGet("api/flags")]
    public async Task<IActionResult> List(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId,
        [FromQuery] string[]? flags, [FromQuery] string? accountId, [FromQuery] string? scope, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (ListRefusal(length, orderBy, orderDir) is { } refusal) return BadRequest(new { errors = new[] { refusal, RequestInvalid } });
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return BadRequest(new { errors = new[] { rangeError, RangeInvalid } });

        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        var onlyFlagged = !string.Equals(scope, ScopeAll, StringComparison.OrdinalIgnoreCase);
        using var response = await SendGatewayAsync(HttpMethod.Get, ListPath(range.From!, range.To!, resourceId, flags, accountId, onlyFlagged, page, size), null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        if (await PageOf(response, ct) is not { } matched) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });

        var total = matched.Total;
        var narrowed = onlyFlagged || !string.IsNullOrWhiteSpace(resourceId) || !string.IsNullOrWhiteSpace(accountId) || (flags ?? []).Any(f => !string.IsNullOrWhiteSpace(f));
        if (narrowed)
        {
            using var all = await SendGatewayAsync(HttpMethod.Get, ListPath(range.From!, range.To!, null, null, null, false, 1, 1), null, ct);
            total = all is { IsSuccessStatusCode: true } && await PageOf(all, ct) is { } whole ? whole.Total : matched.Total;
        }

        return Ok(new { data = new { items = matched.Items, total, filteredTotal = matched.Total, from = range.From, to = range.To } });
    }

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? ListRefusal(int? length, string? orderBy, string? orderDir)
    {
        if (length is { } n && (n < 1 || n > MaxPageSize)) return $"length must be 1–{MaxPageSize}.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list is ordered by the start, newest first; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        return null;
    }

    /// <summary>
    /// Static, so MVC never treats it as an action: the window of a period in UTC days (both ends included) — the twin of
    /// flags-core.js <c>rangeOf</c> / <c>rangeError</c>. "" / last7 = today and the 6 days before; last30 = the 30 days up
    /// to today; month = the 1st of this month to today; custom = <paramref name="from"/> … <paramref name="to"/>
    /// (yyyy-MM-dd, from ≤ to, at most 92 days). An unknown period or a bad custom range → Error.
    /// </summary>
    public static (string? From, string? To, string? Error) ResolveRange(string? period, string? from, string? to, DateOnly today)
    {
        static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        switch ((period ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "":
            case "last7":
                return (Iso(today.AddDays(-6)), Iso(today), null);
            case "last30":
                return (Iso(today.AddDays(-29)), Iso(today), null);
            case "month":
                return (Iso(new DateOnly(today.Year, today.Month, 1)), Iso(today), null);
            case "custom":
                if (!DateOnly.TryParseExact((from ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a)
                    || !DateOnly.TryParseExact((to ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var b))
                {
                    return (null, null, "A custom period needs from and to (yyyy-MM-dd).");
                }

                if (b < a) return (null, null, "from must not be after to.");
                if (b.DayNumber - a.DayNumber + 1 > MaxRangeDays) return (null, null, $"A period is at most {MaxRangeDays} days.");
                return (Iso(a), Iso(b), null);
            default:
                return (null, null, "period must be one of: last7, last30, month, custom.");
        }
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM list path — every value escaped, the flags as ONE csv
    /// (blank entries dropped), the page clamped (1 … ; 1 … 100), empty filters left out.</summary>
    public static string ListPath(string from, string to, string? resourceId, IEnumerable<string>? flags, string? accountId, bool onlyFlagged, int page, int pageSize)
    {
        var q = new List<string> { "from=" + Uri.EscapeDataString(from), "to=" + Uri.EscapeDataString(to) };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) q.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        Add("resourceId", resourceId);
        Add("flags", string.Join(",", (flags ?? []).SelectMany(f => (f ?? string.Empty).Split(',')).Select(f => f.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.Ordinal)));
        Add("accountId", accountId);
        q.Add("onlyFlagged=" + (onlyFlagged ? "true" : "false"));
        q.Add("page=" + Math.Max(1, page));
        q.Add("pageSize=" + Math.Clamp(pageSize, 1, MaxPageSize));
        return Flags + "?" + string.Join("&", q);
    }

    // ---------------- the summary (the card + the rep summary, client mode) ----------------

    /// <summary>
    /// The summary card and the rep summary (a bounded set: the caller's team, or the tenant's reps with read-all) in ONE
    /// read: the same period / rep filter as the list, translated the same way. The whole rep set comes back — no page
    /// size is ever sent (client mode).
    /// </summary>
    [HttpGet("api/summary")]
    public async Task<IActionResult> Summary([FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return BadRequest(new { errors = new[] { rangeError, RangeInvalid } });
        return await ProxyAsync(HttpMethod.Get, SummaryPath(range.From!, range.To!, resourceId), null, true, ct);
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM summary path.</summary>
    public static string SummaryPath(string from, string to, string? resourceId)
        => Flags + "/summary?from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to)
           + (string.IsNullOrWhiteSpace(resourceId) ? string.Empty : "&resourceId=" + Uri.EscapeDataString(resourceId.Trim()));

    [HttpGet("api/flags/{executionId:guid}")]
    public Task<IActionResult> Detail(Guid executionId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Flags}/{executionId:D}", null, MayRead, ct);

    // ---------------- the export (BL-452: the file is the screen) ----------------

    /// <summary>The export's columns: the list's own data keys, in the screen's words (the request culture).</summary>
    public static readonly IReadOnlyList<string> ExportKeys = ["startedAt", "repName", "target", "kind", "flags", "durationSeconds", "exceptionId"];

    /// <summary>
    /// <c>GET api/flags/export?format=csv|xlsx&amp;columns=…</c> + the list's own filters: EVERY matching visit (the CRM list
    /// read page by page, 100 at a time — the CRM has no export of its own and this package changes nothing there), the
    /// visible columns in screen order, the kind / flags / duration in the reader's language (the chips' words and values,
    /// "QR not scanned" included). More than 50 000 → 413 EXPORT_TOO_LARGE before a single row is written; an unknown
    /// column → 400; a window the CRM would refuse → 400.
    /// </summary>
    [HttpGet("api/flags/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId,
        [FromQuery] string[]? flags, [FromQuery] string? accountId, [FromQuery] string? scope, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        var set = ExportColumns();
        if (!set.TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        if (ListRefusal(null, orderBy, orderDir) is { } refusal) return ExportRefusal(400, refusal, RequestInvalid);
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return ExportRefusal(400, rangeError, RangeInvalid);
        var onlyFlagged = !string.Equals(scope, ScopeAll, StringComparison.OrdinalIgnoreCase);

        var rows = new List<JsonElement>();
        for (var page = 1; ; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, ListPath(range.From!, range.To!, resourceId, flags, accountId, onlyFlagged, page, MaxPageSize), null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            if (await PageOf(response, ct) is not { } chunk) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
            if (chunk.Total > ListExportContract.MaxRows)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                    $"{chunk.Total} visits match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter.", ListExportContract.TooLargeCode);
            }

            rows.AddRange(chunk.Items.EnumerateArray());
            if (chunk.Items.GetArrayLength() < MaxPageSize || rows.Count >= chunk.Total) break;
        }

        var lines = rows.Select(r => ExportRowOf(r, culture)).ToList();
        var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text("PageTitle", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("visit-evidence-flags", DateTimeOffset.UtcNow, fileFormat));
    }

    /// <summary>One visit as the export writes it (the words already in the request culture).</summary>
    public sealed record FlagsExportRow(DateTimeOffset? StartedAt, string Rep, string Target, string Kind, string Flags, string Duration, string Exception);

    private FlagsExportRow ExportRowOf(JsonElement row, CultureInfo culture)
    {
        var target = row.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.Object ? t : default;
        var name = target.ValueKind == JsonValueKind.Object ? Str(target, "name") : null;
        var institution = target.ValueKind == JsonValueKind.Object ? Str(target, "accountName") : null;
        var doctor = target.ValueKind == JsonValueKind.Object && Str(target, "type") == "contact";
        var targetText = doctor ? string.Join(" · ", new[] { institution, name }.Where(x => !string.IsNullOrWhiteSpace(x))) : (name ?? institution ?? string.Empty);
        var kind = Str(row, "kind");
        var started = row.TryGetProperty("startedAt", out var s) && s.ValueKind == JsonValueKind.String && s.TryGetDateTimeOffset(out var at) ? at : (DateTimeOffset?)null;
        var duration = Num(row, "durationSeconds");
        return new FlagsExportRow(
            started,
            Str(row, "repName") ?? Str(row, "resourceId") ?? string.Empty,
            targetText,
            string.IsNullOrWhiteSpace(kind) ? string.Empty : Text("Kind_" + kind, culture),
            string.Join(", ", ChipTexts(row, culture)),
            duration is { } d ? DurationText(d, culture) : string.Empty,
            string.IsNullOrWhiteSpace(Str(row, "exceptionId")) ? string.Empty : Text("ExceptionLink", culture));
    }

    /// <summary>The row's chips as the screen writes them (flags-core.js chipsOf): the flags in the screen's order with their
    /// value (distance / accuracy / duration), then "QR not scanned" when the start's QR verdict is missing.</summary>
    private IEnumerable<string> ChipTexts(JsonElement row, CultureInfo culture)
    {
        var codes = row.TryGetProperty("flags", out var f) && f.ValueKind == JsonValueKind.Array
            ? f.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];
        var location = row.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.Object ? l : default;
        foreach (var code in codes.OrderBy(c => Array.IndexOf(ChipOrder, c) is var i && i < 0 ? 99 : i))
        {
            var label = Text("Flag_" + code, culture);
            string? value = code switch
            {
                "location_far" when location.ValueKind == JsonValueKind.Object && Num(location, "distanceMeters") is { } m => DistanceText(m, culture),
                "location_inaccurate" when location.ValueKind == JsonValueKind.Object && Num(location, "accuracyMeters") is { } m => DistanceText(m, culture),
                "short_visit" when Num(row, "durationSeconds") is { } sec => DurationText(sec, culture),
                _ => null
            };
            yield return value is null ? label : label + " · " + value;
        }

        if (row.TryGetProperty("qr", out var qr) && qr.ValueKind == JsonValueKind.Object && Str(qr, "status") == "missing")
        {
            yield return Text("Chip_qr_missing", culture);
        }
    }

    /// <summary>The chip order of flags-core.js FLAGS (device → QR → location → duration → exception).</summary>
    public static readonly string[] ChipOrder =
    [
        "device_unverified", "mock_location", "device_integrity_failed", "qr_not_issued", "qr_wrong_account", "qr_revoked", "qr_invalid",
        "qr_unverifiable", "location_far", "location_missing", "location_inaccurate", "account_location_unknown", "short_visit", "started_with_exception"
    ];

    /// <summary>flags-core.js distance (G-8): under 1000 m whole metres, from 1000 m kilometres with one decimal.</summary>
    private string DistanceText(double meters, CultureInfo culture)
        => meters < 1000
            ? Fill(Text("UnitMeters", culture), Math.Round(meters).ToString("0", culture))
            : Fill(Text("UnitKm", culture), (Math.Round(meters / 100) / 10).ToString("0.0", culture));

    /// <summary>flags-core.js duration: "40 sn" / "1 dk 40 sn" / "18 dk" (from 10 minutes rounded to whole minutes).</summary>
    private string DurationText(double seconds, CultureInfo culture)
    {
        var s = (int)Math.Max(0, Math.Round(seconds));
        int m = s / 60, r = s % 60;
        if (m == 0) return Fill(Text("Seconds", culture), r.ToString(culture));
        return r == 0 || m >= 10
            ? Fill(Text("Minutes", culture), (m >= 10 ? (int)Math.Round(s / 60.0) : m).ToString(culture))
            : Fill(Text("MinSec", culture), m.ToString(culture), r.ToString(culture));
    }

    private static string Fill(string template, params string[] args)
    {
        var text = template;
        for (var i = 0; i < args.Length; i++) text = text.Replace("{" + i + "}", args[i], StringComparison.Ordinal);
        return text;
    }

    private static double? Num(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : null;

    private ListExportColumnSet<FlagsExportRow> ExportColumns() => new(
    [
        new("startedAt", c => Text("ColStart", c), (r, c) => r.StartedAt?.ToString("g", c)),
        new("repName", c => Text("ColRep", c), (r, _) => r.Rep),
        new("target", c => Text("ColTarget", c), (r, _) => r.Target),
        new("kind", c => Text("ColKind", c), (r, _) => r.Kind),
        new("flags", c => Text("ColFlags", c), (r, _) => r.Flags),
        new("durationSeconds", c => Text("ColDuration", c), (r, _) => r.Duration),
        new("exceptionId", c => Text("ColException", c), (r, _) => r.Exception)
    ]);

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<VisitEvidenceFlagsIndex>)) as IStringLocalizer<VisitEvidenceFlagsIndex>;
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

    // ---------------- the institution filter (G-7) + the photo ----------------

    /// <summary>The most institutions one <see cref="AccountLabels"/> call reads (the filter's same-name ones only).</summary>
    public const int MaxAccountLabels = 50;

    /// <summary>
    /// W3E-FIX (G-7) — the institution filter tells same-name institutions apart: for the ids the page names (only those
    /// whose name another one in the list also carries, ≤ 50) the institution code and the city in words (G-3). Each is
    /// the CRM's own account read with the caller's session (the tenant's, its keys); an institution it does not answer
    /// for is simply left out — the option then keeps its plain name. Never a payload, never a person.
    /// </summary>
    [HttpGet("api/account-labels")]
    public async Task<IActionResult> AccountLabels([FromQuery] string? ids, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var list = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().Take(MaxAccountLabels).ToList();
        if (list.Count == 0) return Ok(new { data = Array.Empty<object>() });
        var cities = await ReadReferenceLabelsAsync("city", ct);
        var rows = new List<object>();
        foreach (var id in list)
        {
            if (await ReadDataAsync($"/api/crm/accounts/{id:D}", ct) is not { ValueKind: JsonValueKind.Object } account) continue;
            rows.Add(new { accountId = id, code = Str(account, "accountCode"), city = CrmCityLabels.Resolve(Str(account, "cityRef"), cities) });
        }

        return Ok(new { data = rows });
    }

    /// <summary>The start photo (bytes) through the existing evidence stream, with its own keys (never the flags key alone).</summary>
    [HttpGet("api/flags/{executionId:guid}/photo")]
    public Task<IActionResult> Photo(Guid executionId, CancellationToken ct)
        => ProxyBytesAsync($"/api/crm/visit-execution/{executionId:D}/photo", MayRead && MaySeePhotos, ct);
}
