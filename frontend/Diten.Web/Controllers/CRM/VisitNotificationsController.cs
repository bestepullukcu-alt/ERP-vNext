using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitNotifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W5 (W5-WEB) — <c>/CRM/VisitNotifications</c>: the sent visit e-mails (manifest page <c>CRM_VISIT_NOTIFICATIONS</c>,
/// mockup 06-epostalar): the Golden Compact list, the e-mail preview in the quick view and — for
/// <c>crm.visit-notifications.settings.manage</c> only — the notification settings card. Every call is a same-origin proxy
/// to the W5-BE endpoints with the caller's session; the CRM decides the scope (K-N9: a rep their own visits, a manager the
/// team, the settings key the tenant; outside it 404).
/// <para>
/// The list speaks the list contract (start · length · draw · period / from / to · event · status · resourceId) and the
/// CRM keeps ITS contract (from / to on the visit day, eventCode, status, resourceId, page / pageSize ≤ 100, newest first).
/// The CRM row names no doctor / institution and no recipient (W5-BE): the proxy reads each row's detail of the page
/// (bounded, in parallel) and adds <c>target</c> + <c>recipients</c> — names and roles only, the detail's variables are
/// never passed through whole. No e-mail address ever reaches the browser from here: the CRM stores none for a
/// notification and the settings addresses are read only by the settings card (<see cref="SettingsManage"/>).
/// </para>
/// <para>
/// The event codes: the Platform catalogue has no hyphen (<c>crm.visitreport.submitted</c> — §37 PLT-NOTIFY), the CRM wrote
/// the hyphenated form until W5-FIX. The page filters by the event KEY (<see cref="EventKeys"/>); the proxy asks with the
/// catalogue code and, if the CRM refuses it (still the old constants), once more with the old one.
/// </para>
/// </summary>
[Authorize]
[Route("CRM/VisitNotifications")]
public sealed class VisitNotificationsController : CrmGatewayProxy
{
    public const string Read = "crm.visit-notifications.read";
    public const string SettingsManage = "crm.visit-notifications.settings.manage";

    /// <summary>The CRM list's own page cap.</summary>
    public const int MaxPageSize = 100;

    /// <summary>How many row details one list page reads at the same time.</summary>
    public const int DetailParallelism = 6;

    /// <summary>The four events by their key (the filter's values): the catalogue code and the CRM's pre-W5-FIX code.</summary>
    public static readonly IReadOnlyDictionary<string, (string Code, string Legacy)> EventKeys = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["submitted"] = ("crm.visitreport.submitted", "crm.visit-report.submitted"),
        ["adverseevent"] = ("crm.visitreport.adverseevent", "crm.visit-report.adverse-event"),
        ["offlabelrequest"] = ("crm.visitreport.offlabelrequest", "crm.visit-report.off-label-request"),
        ["overdue"] = ("crm.visitreport.overdue", "crm.visit-report.overdue")
    };

    /// <summary>The CRM's statuses (VisitNotificationStatuses).</summary>
    public static readonly IReadOnlyList<string> Statuses = ["sent", "queued", "failed", "no-recipient", "route-pending"];

    private const string Notifications = "/api/crm/visit-notifications";
    private const string RangeInvalid = "visit_notifications_range_invalid";
    private const string RequestInvalid = "visit_notifications_filter_invalid";

    public VisitNotificationsController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitNotificationsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, SettingsManage);

    private bool MayManage => HasAny(SettingsManage);

    private DateOnly Today()
    {
        var clock = HttpContext?.RequestServices?.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>The page. Without a read key the view draws only the access notice (UAS-001, no redirect); without the
    /// settings key the settings tab and card are not rendered at all.</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/VisitNotifications/Index.cshtml", new VisitNotificationsViewModel
    {
        CanRead = MayRead,
        CanManageSettings = MayManage
    });

    // ---------------- the list (server mode) ----------------

    [HttpGet("api/list")]
    public async Task<IActionResult> List(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? @event, [FromQuery] string? status,
        [FromQuery] string? resourceId, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (ListRefusal(length, search, orderBy, orderDir, @event, status) is { } refusal) return BadRequest(new { errors = new[] { refusal, RequestInvalid } });
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return BadRequest(new { errors = new[] { rangeError, RangeInvalid } });

        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        var (response, items, total) = await ReadPageAsync(range.From!, range.To!, @event, status, resourceId, page, size, ct);
        if (items is null) return await ToProxyResultAsync(response, ct);
        response?.Dispose();

        var enriched = await EnrichAsync(items, ct);
        var narrowed = !string.IsNullOrWhiteSpace(@event) || !string.IsNullOrWhiteSpace(status) || !string.IsNullOrWhiteSpace(resourceId);
        var all = total;
        if (narrowed)
        {
            var (wholeResponse, wholeItems, wholeTotal) = await ReadPageAsync(range.From!, range.To!, null, null, null, 1, 1, ct);
            wholeResponse?.Dispose();
            if (wholeItems is not null) all = wholeTotal;
        }

        return Ok(new { data = new { items = enriched, total = all, filteredTotal = total, from = range.From, to = range.To } });
    }

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? ListRefusal(int? length, string? search, string? orderBy, string? orderDir, string? eventKey, string? status)
    {
        if (length is { } n && (n < 1 || n > MaxPageSize)) return $"length must be 1–{MaxPageSize}.";
        if (!string.IsNullOrWhiteSpace(search)) return "The e-mail list has no text search.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list is ordered newest first; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        if (!string.IsNullOrWhiteSpace(eventKey) && !EventKeys.ContainsKey(eventKey.Trim())) return "event must be one of: " + string.Join(", ", EventKeys.Keys) + ".";
        if (!string.IsNullOrWhiteSpace(status) && !Statuses.Contains(status.Trim())) return "status must be one of: " + string.Join(", ", Statuses) + ".";
        return null;
    }

    /// <summary>
    /// Static, so MVC never treats it as an action: the window of a period in UTC days (both ends included) on the VISIT
    /// day (the CRM filters on it) — the twin of notifications-core.js <c>rangeOf</c>. "" / last30 = the 30 days up to today;
    /// last7; month = the 1st of this month to today; custom = from … to (yyyy-MM-dd, from ≤ to).
    /// </summary>
    public static (string? From, string? To, string? Error) ResolveRange(string? period, string? from, string? to, DateOnly today)
    {
        static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        switch ((period ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "":
            case "last30":
                return (Iso(today.AddDays(-29)), Iso(today), null);
            case "last7":
                return (Iso(today.AddDays(-6)), Iso(today), null);
            case "month":
                return (Iso(new DateOnly(today.Year, today.Month, 1)), Iso(today), null);
            case "custom":
                if (!DateOnly.TryParseExact((from ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a)
                    || !DateOnly.TryParseExact((to ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var b))
                {
                    return (null, null, "A custom period needs from and to (yyyy-MM-dd).");
                }

                return b < a ? (null, null, "from must not be after to.") : (Iso(a), Iso(b), null);
            default:
                return (null, null, "period must be one of: last7, last30, month, custom.");
        }
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM list path — every value escaped, empty filters left
    /// out, the page clamped (1 … ; 1 … 100).</summary>
    public static string ListPath(string from, string to, string? eventCode, string? status, string? resourceId, int page, int pageSize)
    {
        var q = new List<string> { "from=" + Uri.EscapeDataString(from), "to=" + Uri.EscapeDataString(to) };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) q.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        Add("eventCode", eventCode);
        Add("status", status);
        Add("resourceId", resourceId);
        q.Add("page=" + Math.Max(1, page));
        q.Add("pageSize=" + Math.Clamp(pageSize, 1, MaxPageSize));
        return Notifications + "?" + string.Join("&", q);
    }

    /// <summary>One CRM page. With an event key: the catalogue code first; a 400 (the CRM still on its pre-W5-FIX
    /// constants) → once more with the old hyphenated code. Items null = the CRM's answer is the result.</summary>
    private async Task<(HttpResponseMessage? Response, List<JsonObject>? Items, long Total)> ReadPageAsync(
        string from, string to, string? eventKey, string? status, string? resourceId, int page, int size, CancellationToken ct)
    {
        var codes = string.IsNullOrWhiteSpace(eventKey) ? new string?[] { null } : new string?[] { EventKeys[eventKey.Trim()].Code, EventKeys[eventKey.Trim()].Legacy };
        HttpResponseMessage? response = null;
        foreach (var code in codes)
        {
            response?.Dispose();
            response = await SendGatewayAsync(HttpMethod.Get, ListPath(from, to, code, status, resourceId, page, size), null, ct);
            if (response is { StatusCode: System.Net.HttpStatusCode.BadRequest } && code is not null && code != codes[^1]) continue;
            break;
        }

        if (response is null || !response.IsSuccessStatusCode) return (response, null, 0);
        try
        {
            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            var data = root is JsonObject o && o["data"] is JsonObject d ? d : root as JsonObject;
            if (data?["items"] is not JsonArray array) return (response, null, 0);
            var total = data["total"] is JsonValue t && t.TryGetValue<long>(out var n) ? n : array.Count;
            return (response, array.OfType<JsonObject>().Select(x => (JsonObject)x.DeepClone()).ToList(), total);
        }
        catch (JsonException)
        {
            return (response, null, 0);
        }
    }

    /// <summary>
    /// Each row of the page + its detail's doctor / institution names and recipient spec (role · kind · mailbox · the user
    /// whose manager). A detail that cannot be read leaves the row as the CRM sent it (the page then derives the roles
    /// from the event). Never the variables whole, never an address.
    /// </summary>
    private async Task<List<JsonObject>> EnrichAsync(List<JsonObject> items, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(DetailParallelism);
        var tasks = items.Select(async item =>
        {
            var id = item["id"]?.GetValue<string>();
            if (!Guid.TryParse(id, out var guid)) return item;
            await gate.WaitAsync(ct);
            try
            {
                if (await ReadDataAsync($"{Notifications}/{guid:D}", ct) is { ValueKind: JsonValueKind.Object } detail && detail.TryGetProperty("row", out _))
                {
                    var vars = detail.TryGetProperty("variables", out var v) && v.ValueKind == JsonValueKind.Object ? v : default;
                    item["target"] = new JsonObject
                    {
                        ["doctorName"] = vars.ValueKind == JsonValueKind.Object ? Str(vars, "doctorName") : null,
                        ["institutionName"] = vars.ValueKind == JsonValueKind.Object ? Str(vars, "institutionName") : null
                    };
                    item["recipients"] = Recipients(detail, "recipients", "role", "kind", "userId", "mailbox");
                    item["resolved"] = Recipients(detail, "resolved", "role", "userId", "via");
                }
            }
            finally
            {
                gate.Release();
            }

            return item;
        }).ToList();
        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>Only the named fields of each entry (an allow-list: nothing else of the CRM's shape is passed on).</summary>
    private static JsonArray Recipients(JsonElement detail, string property, params string[] fields)
    {
        var list = new JsonArray();
        if (!detail.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return list;
        foreach (var entry in array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            var o = new JsonObject();
            foreach (var field in fields) o[field] = Str(entry, field);
            list.Add(o);
        }

        return list;
    }

    // ---------------- the preview ----------------

    /// <summary>
    /// The quick view's e-mail preview: the row, the recipient spec + what the Platform resolved, and the template
    /// variables — rebuilt from an allow-list (no e-mail address field can pass even if the CRM grew one), the last error
    /// left out (a provider's error text may name an address). 404 outside the caller's scope (the CRM's).
    /// </summary>
    [HttpGet("api/list/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        using var response = await SendGatewayAsync(HttpMethod.Get, $"{Notifications}/{id:D}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Object) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
            return Ok(new { data = PreviewOf(data) });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        }
    }

    /// <summary>The template variables the preview draws (the W5-BE contract annex) — nothing else is passed on.</summary>
    public static readonly IReadOnlyList<string> PreviewVariables =
    [
        "repName", "doctorName", "institutionName", "visitDate", "reportUrl", "products", "note", "adverseEvent", "offLabelRequest",
        "followUpDate", "adverseEventNote", "deadline"
    ];

    /// <summary>The row fields the page reads.</summary>
    public static readonly IReadOnlyList<string> RowFields =
        ["id", "eventCode", "status", "plannedVisitId", "visitReportId", "resourceId", "resourceName", "visitDate", "createdAt", "sentAt", "attempts", "notes"];

    /// <summary>Static, so MVC never treats it as an action: the detail rebuilt from allow-lists.</summary>
    public static JsonObject PreviewOf(JsonElement detail)
    {
        var row = new JsonObject();
        if (detail.TryGetProperty("row", out var r) && r.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in RowFields)
            {
                if (r.TryGetProperty(field, out var value)) row[field] = JsonNode.Parse(value.GetRawText());
            }
        }

        var variables = new JsonObject();
        if (detail.TryGetProperty("variables", out var v) && v.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in PreviewVariables)
            {
                if (Str(v, name) is { } text) variables[name] = text;
            }
        }

        return new JsonObject
        {
            ["row"] = row,
            ["recipients"] = Recipients(detail, "recipients", "role", "kind", "userId", "mailbox"),
            ["resolved"] = Recipients(detail, "resolved", "role", "userId", "via"),
            ["variables"] = variables
        };
    }

    // ---------------- the settings (the card: settings.manage only) ----------------

    /// <summary>The PV / MSL mailboxes and the switches. Only the settings key reads them here (the addresses are shown to
    /// nobody else — the CRM lets the read key read them too, this page does not ask).</summary>
    [HttpGet("api/settings")]
    public Task<IActionResult> GetSettings(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, Notifications + "/settings", null, MayManage, ct);

    /// <summary>The save: settings.manage, checked HERE before the CRM is asked (the card is not drawn without it, and the
    /// endpoint does not trust that); the CRM answers 409 when the version moved, 400 for a bad address.</summary>
    [HttpPut("api/settings")]
    public async Task<IActionResult> PutSettings(CancellationToken ct)
    {
        if (!MayManage) return Denied();
        return await ProxyAsync(HttpMethod.Put, Notifications + "/settings", await ReadBodyAsync(ct) ?? "{}", true, ct);
    }

    // ---------------- the export (BL-452: the file is the screen) ----------------

    /// <summary>The export's columns: the list's own data keys (the institution / doctor is read per row on screen only).</summary>
    public static readonly IReadOnlyList<string> ExportKeys = ["createdAt", "eventCode", "resourceName", "recipients", "status"];

    [HttpGet("api/list/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? @event, [FromQuery] string? status,
        [FromQuery] string? resourceId, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        if (!ExportColumns().TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        if (ListRefusal(null, search, orderBy, orderDir, @event, status) is { } refusal) return ExportRefusal(400, refusal, RequestInvalid);
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return ExportRefusal(400, rangeError, RangeInvalid);

        var rows = new List<JsonObject>();
        for (var page = 1; ; page++)
        {
            var (response, chunk, total) = await ReadPageAsync(range.From!, range.To!, @event, status, resourceId, page, MaxPageSize, ct);
            if (chunk is null) return await ToProxyResultAsync(response, ct);
            response?.Dispose();
            if (total > ListExportContract.MaxRows)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                    $"{total} e-mails match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter.", ListExportContract.TooLargeCode);
            }

            rows.AddRange(chunk);
            if (chunk.Count < MaxPageSize || rows.Count >= total) break;
        }

        var lines = rows.Select(r => ExportRowOf(r, culture)).ToList();
        var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text("PageTitle", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("visit-notifications", DateTimeOffset.UtcNow, fileFormat));
    }

    public sealed record NotificationExportRow(DateTimeOffset? CreatedAt, string Event, string Rep, string Recipients, string Status);

    /// <summary>Static, so MVC never treats it as an action: the event key of a code, hyphenated or not (the twin of
    /// notifications-core.js eventKey).</summary>
    public static string? EventKeyOf(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        const string prefix = "crm.visitreport.";
        return c.StartsWith(prefix, StringComparison.Ordinal) && EventKeys.ContainsKey(c[prefix.Length..]) ? c[prefix.Length..] : null;
    }

    /// <summary>Static, so MVC never treats it as an action: the recipient ROLE keys of a row without its detail — the
    /// CRM's spec per event (K-N4) with a pending mailbox left out and "cc" moved up when "to" is empty (the twin of
    /// notifications-core.js recipientsOf + roleKeys); nobody resolved = no role claimed.</summary>
    public static IReadOnlyList<string> RoleKeysOf(string? eventKey, string? status, IReadOnlyCollection<string> notes)
    {
        if (status == "no-recipient") return ["RcpNone"];
        var mailbox = eventKey switch { "adverseevent" => "pv", "offlabelrequest" => "msl", _ => null };
        if (mailbox is null) return ["Rcp_manager"];
        return notes.Contains(mailbox + "-route-pending") ? ["Rcp_manager"] : ["Rcp_" + mailbox, "Rcp_manager"];
    }

    private NotificationExportRow ExportRowOf(JsonObject row, CultureInfo culture)
    {
        string? S(string name) => row[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var notes = row["notes"] is JsonArray a ? a.Select(x => x?.GetValue<string>() ?? string.Empty).ToList() : [];
        var key = EventKeyOf(S("eventCode"));
        var status = S("status") ?? string.Empty;
        var statusKey = status switch
        {
            "route-pending" => notes.Contains("msl-route-pending") && !notes.Contains("pv-route-pending") ? "St_route_pending_msl" : "St_route_pending_pv",
            "failed" when notes.Contains("attempts-exhausted") => "St_failed_final",
            "no-recipient" => "St_no_recipient",
            "sent" or "queued" or "failed" => "St_" + status,
            _ => null
        };
        var created = DateTimeOffset.TryParse(S("sentAt") ?? S("createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTimeOffset?)null;
        return new NotificationExportRow(
            created,
            key is null ? S("eventCode") ?? string.Empty : Text("Event_" + key, culture),
            S("resourceName") ?? S("resourceId") ?? string.Empty,
            string.Join(", ", RoleKeysOf(key, status, notes).Select(k => Text(k, culture))),
            statusKey is null ? status : Text(statusKey, culture));
    }

    private ListExportColumnSet<NotificationExportRow> ExportColumns() => new(
    [
        new("createdAt", c => Text("ColDate", c), (r, c) => r.CreatedAt?.ToString("g", c)),
        new("eventCode", c => Text("ColEvent", c), (r, _) => r.Event),
        new("resourceName", c => Text("ColRep", c), (r, _) => r.Rep),
        new("recipients", c => Text("ColRecipients", c), (r, _) => r.Recipients),
        new("status", c => Text("ColStatus", c), (r, _) => r.Status)
    ]);

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<VisitNotificationsIndex>)) as IStringLocalizer<VisitNotificationsIndex>;
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
}
