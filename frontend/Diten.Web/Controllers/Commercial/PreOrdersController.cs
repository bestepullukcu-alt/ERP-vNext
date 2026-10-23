using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.Commercial;
using Diten.Web.Views.Commercial.PreOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-a) — <c>/Commercial/PreOrders</c>: the MOD-0168 pharmacy pre-order screens (mockup W6 A–E, DESIGN-GAP-W6
/// §1): B the Golden Compact list (manifest page <c>PREORDERS</c>), A create / edit (a responsive page for tablet and phone),
/// Details, C the approval (the Work Center deep link <c>Approve/{id}</c>), C-link the e-mail link (<c>ApprovalLink?token=</c>,
/// AFTER sign-in — D4) and E the depot document. Every call is a same-origin proxy to the W6-BE endpoints
/// (<c>/api/commercial/pre-orders/**</c>) with the caller's session: the CRM computes every price, limit and total (the page
/// only shows them — MOD-0168 §4.2), decides the scope (own / team / approver / read-all; outside it 404) and checks every
/// write again. The page keys below are checked HERE too, before the CRM is asked (a hidden button's write is refused).
/// <para>
/// The two e-mail link paths are the CRM's constants (W6-BE-c1, <c>ConfigurationPreOrderLinks</c>): <c>Details/{id}</c> and
/// <c>ApprovalLink?token=</c>; the Work Center's is <c>Approve/{id}</c> (<c>PreOrderApprovalDefaults.Link</c>). A test pins the
/// three against the CRM source.
/// </para>
/// </summary>
[Authorize]
[Route("Commercial/PreOrders")]
public sealed class PreOrdersController : CrmGatewayProxy
{
    public const string Read = "commercial.pre-orders.read";
    public const string ReadAll = "commercial.pre-orders.read-all";
    public const string Create = "commercial.pre-orders.create";
    public const string Submit = "commercial.pre-orders.submit";
    public const string Approve = "commercial.pre-orders.approve";
    public const string Cancel = "commercial.pre-orders.cancel";
    public const string RecordDepotResponse = "commercial.pre-orders.record-depot-response";
    public const string RetryTransmission = "commercial.pre-orders.retry-transmission";
    public const string Export = "commercial.pre-orders.export";

    /// <summary>The Web paths the CRM writes into its e-mails / the Work Center task (W6-BE-c1 + W6-BE-a constants).</summary>
    public const string DetailsPath = "/Commercial/PreOrders/Details/";
    public const string ApprovalLinkPath = "/Commercial/PreOrders/ApprovalLink?token=";
    public const string ApprovePath = "/Commercial/PreOrders/Approve/";

    /// <summary>The CRM list's own page cap (PreOrderLimits.PageSizeMax).</summary>
    public const int MaxPageSize = 100;

    /// <summary>How many rows an export carries at most (PreOrderLimits.ExportMax).</summary>
    public const int ExportMax = 5000;

    /// <summary>The CRM's statuses (PreOrderStatuses.All).</summary>
    public static readonly IReadOnlyList<string> Statuses = ["draft", "pending_approval", "approved", "transmitted", "transmission_failed", "rejected", "cancelled"];

    /// <summary>The two document copies (W6-BE-c2).</summary>
    public static readonly IReadOnlyList<string> Copies = ["depot", "pharmacy"];

    /// <summary>The account types of a depot (K-W13): a distributor or a wholesaler.</summary>
    public static readonly IReadOnlyList<string> DepotTypes = ["distributor", "wholesaler"];

    private const string Orders = "/api/commercial/pre-orders";
    private const string Links = "/api/commercial/pre-order-approval-links";
    private const string CitySet = "city";
    private const string FilterInvalid = "pre_order_filter_invalid";
    private const string RangeInvalid = "pre_order_range_invalid";

    public PreOrdersController(HttpClient httpClient, IConfiguration configuration, ILogger<PreOrdersController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, ReadAll);

    private DateOnly Today()
    {
        var clock = HttpContext?.RequestServices?.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>Static, so MVC never treats it as an action: the flags of one screen from the caller's keys.</summary>
    public static PreOrderPageViewModel Flags(Func<string, bool> has, string screen, Guid? orderId = null)
        => new()
        {
            CanRead = has(Read) || has(ReadAll),
            CanReadAll = has(ReadAll),
            CanCreate = has(Create),
            CanSubmit = has(Submit),
            CanApprove = has(Approve),
            CanCancel = has(Cancel),
            CanRecordDepotResponse = has(RecordDepotResponse),
            CanRetryTransmission = has(RetryTransmission),
            CanExport = has(Export),
            SeesTeam = has(ReadAll) || has(Approve),
            Screen = screen,
            OrderId = orderId
        };

    private PreOrderPageViewModel FlagsFor(string screen, Guid? orderId = null)
        => Flags(key => HasAny(key), screen, orderId);

    // ═══ the pages ═══════════════════════════════════════════════════════════════════════════════════════════════
    // Each view draws ONLY the access notice when the flag of its screen is false (UAS-001).

    /// <summary>B — the list (read | read-all).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Commercial/PreOrders/Index.cshtml", FlagsFor("list"));

    /// <summary>A — a new draft (create). <paramref name="visitId"/> = opened from a visit (optional).</summary>
    [HttpGet("Create")]
    public IActionResult CreatePage([FromQuery] Guid? visitId)
    {
        var model = FlagsFor("create");
        model.VisitId = visitId;
        return View("~/Views/Commercial/PreOrders/Create.cshtml", model);
    }

    /// <summary>A — an existing order (create): a draft is edited, any other status is shown locked with its timeline.</summary>
    [HttpGet("Edit/{id:guid}")]
    public IActionResult EditPage(Guid id) => View("~/Views/Commercial/PreOrders/Edit.cshtml", FlagsFor("edit", id));

    /// <summary>Details — read only (read | read-all); the e-mail link of the representative lands here (W6-BE-c1).</summary>
    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id) => View("~/Views/Commercial/PreOrders/Details.cshtml", FlagsFor("details", id));

    /// <summary>C — the approval (approve); the Work Center deep link (MOD-0168 §4.5).</summary>
    [HttpGet("Approve/{id:guid}")]
    public IActionResult ApprovePage(Guid id) => View("~/Views/Commercial/PreOrders/Approve.cshtml", FlagsFor("approve", id));

    /// <summary>
    /// C-link — the approver's e-mail link. [Authorize] signs the user in FIRST (D4: no decision without a session); the
    /// token is resolved by the page through <see cref="ResolveLink"/> and sent back with the decision — never stored.
    /// </summary>
    [HttpGet("ApprovalLink")]
    public IActionResult ApprovalLink([FromQuery] string? token)
    {
        var model = FlagsFor("link");
        model.LinkToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        return View("~/Views/Commercial/PreOrders/ApprovalLink.cshtml", model);
    }

    /// <summary>E — the depot document of one order (read | read-all): depot copy ↔ pharmacy copy, the transmission trail.</summary>
    [HttpGet("Document/{id:guid}")]
    public IActionResult DocumentPage(Guid id) => View("~/Views/Commercial/PreOrders/Document.cshtml", FlagsFor("document", id));

    // ═══ B · the list (server mode) ════════════════════════════════════════════════════════════════════════════

    /// <summary>The list query of the page (the list contract's names) — one record so the list and the export share it.</summary>
    public sealed record ListFilter(string? Period, string? From, string? To, string? Status, string? Rep, string? Pharmacy, string? Depot,
        string? Sku, string? Country, string? Awaiting);

    [HttpGet("api/list")]
    public async Task<IActionResult> List(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? status, [FromQuery] string? rep,
        [FromQuery] string? pharmacy, [FromQuery] string? depot, [FromQuery] string? sku, [FromQuery] string? country, [FromQuery] string? awaiting,
        CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var filter = new ListFilter(period, from, to, status, rep, pharmacy, depot, sku, country, awaiting);
        if (ListRefusal(length, orderBy, orderDir, filter) is { } refusal) return BadRequest(new { errors = new[] { refusal, FilterInvalid } });
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return BadRequest(new { errors = new[] { rangeError, RangeInvalid } });

        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        var (response, items, total) = await ReadPageAsync(ListPath(range.From, range.To, filter, search, page, size), ct);
        if (items is null) return await ToProxyResultAsync(response, ct);
        response?.Dispose();

        var cities = items.Any(i => !string.IsNullOrWhiteSpace(i["pharmacyCityRef"]?.GetValue<string>()))
            ? await ReadReferenceLabelsAsync(CitySet, ct)
            : new Dictionary<string, string>();
        var rows = items.Select(i => RowOf(i, cities)).ToList();
        var all = total;
        if (Narrowed(filter, range.From, search))
        {
            var (wholeResponse, wholeItems, wholeTotal) = await ReadPageAsync(ListPath(null, null, new ListFilter(null, null, null, null, null, null, null, null, null, null), null, 1, 1), ct);
            wholeResponse?.Dispose();
            if (wholeItems is not null) all = wholeTotal;
        }

        return Ok(new { data = new { items = rows, total = all, filteredTotal = total } });
    }

    /// <summary>"Onayımı bekleyenler" — how many orders wait for the caller's decision (the filter chip's number).</summary>
    [HttpGet("api/awaiting-count")]
    public async Task<IActionResult> AwaitingCount(CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (!HasAny(Approve)) return Ok(new { data = new { count = 0 } });
        var (response, items, total) = await ReadPageAsync(Orders + "?awaitingMe=true&page=1&pageSize=1", ct);
        if (items is null) return await ToProxyResultAsync(response, ct);
        response?.Dispose();
        return Ok(new { data = new { count = total } });
    }

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? ListRefusal(int? length, string? orderBy, string? orderDir, ListFilter f)
    {
        if (length is { } n && (n < 1 || n > MaxPageSize)) return $"length must be 1–{MaxPageSize}.";
        // The list is ordered newest first by the CRM; a column order (e.g. by total across currencies) is never asked (AC-UI-2).
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list is ordered newest first; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        if (!string.IsNullOrWhiteSpace(f.Status) && !Statuses.Contains(f.Status.Trim())) return "status must be one of: " + string.Join(", ", Statuses) + ".";
        foreach (var (name, value) in new[] { ("rep", f.Rep), ("pharmacy", f.Pharmacy), ("depot", f.Depot), ("sku", f.Sku) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !Guid.TryParse(value.Trim(), out _)) return name + " must be an id.";
        }

        if (!string.IsNullOrWhiteSpace(f.Country) && !System.Text.RegularExpressions.Regex.IsMatch(f.Country.Trim(), "^[A-Za-z]{2}$")) return "country must be a two-letter code.";
        if (!string.IsNullOrWhiteSpace(f.Awaiting) && f.Awaiting.Trim() is not ("true" or "false")) return "awaiting must be true or false.";
        return null;
    }

    /// <summary>
    /// Static, so MVC never treats it as an action: the window of a period in UTC days (both ends included) on the order's
    /// creation day. "" = no window (every order); last7; last30; month = the 1st of this month to today; custom = from … to
    /// (yyyy-MM-dd, from ≤ to). The twin of preorders-core.js <c>rangeOf</c>.
    /// </summary>
    public static (string? From, string? To, string? Error) ResolveRange(string? period, string? from, string? to, DateOnly today)
    {
        static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        switch ((period ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "":
                return (null, null, null);
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

                return b < a ? (null, null, "from must not be after to.") : (Iso(a), Iso(b), null);
            default:
                return (null, null, "period must be one of: last7, last30, month, custom.");
        }
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM list path — every value escaped, empty filters left out,
    /// the page clamped (1 … ; 1 … 100).</summary>
    public static string ListPath(string? from, string? to, ListFilter f, string? search, int page, int pageSize)
    {
        var q = new List<string>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) q.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        Add("from", from);
        Add("to", to);
        Add("status", f.Status);
        Add("repUserId", f.Rep);
        Add("pharmacyAccountId", f.Pharmacy);
        Add("depotAccountId", f.Depot);
        Add("skuId", f.Sku);
        Add("countryCode", f.Country?.Trim().ToUpperInvariant());
        if (string.Equals(f.Awaiting?.Trim(), "true", StringComparison.Ordinal)) q.Add("awaitingMe=true");
        Add("search", search);
        q.Add("page=" + Math.Max(1, page));
        q.Add("pageSize=" + Math.Clamp(pageSize, 1, MaxPageSize));
        return Orders + "?" + string.Join("&", q);
    }

    private static bool Narrowed(ListFilter f, string? from, string? search)
        => from is not null || !string.IsNullOrWhiteSpace(search) || new[] { f.Status, f.Rep, f.Pharmacy, f.Depot, f.Sku, f.Country }.Any(v => !string.IsNullOrWhiteSpace(v))
           || string.Equals(f.Awaiting?.Trim(), "true", StringComparison.Ordinal);

    private async Task<(HttpResponseMessage? Response, List<JsonObject>? Items, long Total)> ReadPageAsync(string path, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
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

    /// <summary>The row fields the list reads (PreOrderRowDto) — an allow-list; nothing else of the CRM's shape passes.</summary>
    public static readonly IReadOnlyList<string> RowFields =
    [
        "id", "code", "createdAt", "status", "pharmacyAccountId", "pharmacyName", "depotAccountId", "depotName", "repUserId", "repName",
        "lineCount", "priceMode", "currency", "gross", "localCurrencyCode", "localGross", "effectiveDiscountPct", "outcome", "depotResponseStatus",
        "awaitingMe", "sellerCode", "countryCode"
    ];

    /// <summary>Static, so MVC never treats it as an action: one list row from the allow-list + the pharmacy's city in words.</summary>
    public static JsonObject RowOf(JsonObject item, IReadOnlyDictionary<string, string> cities)
    {
        var row = new JsonObject();
        foreach (var field in RowFields)
        {
            if (item[field] is { } value) row[field] = value.DeepClone();
        }

        var cityRef = item["pharmacyCityRef"] is JsonValue c && c.TryGetValue<string>(out var s) ? s : null;
        row["pharmacyCity"] = CrmCityLabels.Resolve(cityRef, cities);
        return row;
    }

    // ═══ the export (BL-452: the file is the screen) ════════════════════════════════════════════════════════════

    /// <summary>The export's columns: the list's own data keys.</summary>
    public static readonly IReadOnlyList<string> ExportKeys =
        ["code", "createdAt", "pharmacyName", "depotName", "repName", "lineCount", "gross", "localGross", "effectiveDiscountPct", "status"];

    [HttpGet("api/list/export")]
    public async Task<IActionResult> ExportList(
        [FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? status, [FromQuery] string? rep,
        [FromQuery] string? pharmacy, [FromQuery] string? depot, [FromQuery] string? sku, [FromQuery] string? country, [FromQuery] string? awaiting,
        CancellationToken ct)
    {
        if (!MayRead || !HasAny(Export)) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        if (!ExportColumns().TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        var filter = new ListFilter(period, from, to, status, rep, pharmacy, depot, sku, country, awaiting);
        if (ListRefusal(null, orderBy, orderDir, filter) is { } refusal) return ExportRefusal(400, refusal, FilterInvalid);
        var range = ResolveRange(period, from, to, Today());
        if (range.Error is { } rangeError) return ExportRefusal(400, rangeError, RangeInvalid);

        var rows = new List<JsonObject>();
        for (var page = 1; ; page++)
        {
            var (response, chunk, total) = await ReadPageAsync(ListPath(range.From, range.To, filter, search, page, MaxPageSize), ct);
            if (chunk is null) return await ToProxyResultAsync(response, ct);
            response?.Dispose();
            if (total > ExportMax)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                    $"{total} pre-orders match; an export carries at most {ExportMax}. Narrow the filter.", ListExportContract.TooLargeCode);
            }

            rows.AddRange(chunk);
            if (chunk.Count < MaxPageSize || rows.Count >= total) break;
        }

        var lines = rows.Select(r => ExportRowOf(r, culture)).ToList();
        var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text("PageTitle", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("pre-orders", DateTimeOffset.UtcNow, fileFormat));
    }

    public sealed record PreOrderExportRow(string Code, DateTimeOffset? CreatedAt, string Pharmacy, string Depot, string Rep, int Lines, string Total,
        string Local, string Effective, string Status);

    /// <summary>Static, so MVC never treats it as an action: an amount with its currency code in <paramref name="culture"/>
    /// (no total across currencies is ever made — AC-UI-2); "" without an amount.</summary>
    public static string Money(decimal? amount, string? currency, CultureInfo culture)
        => amount is { } a ? a.ToString("N2", culture) + (string.IsNullOrWhiteSpace(currency) ? string.Empty : " " + currency.Trim().ToUpperInvariant()) : string.Empty;

    private PreOrderExportRow ExportRowOf(JsonObject row, CultureInfo culture)
    {
        string? S(string name) => row[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        decimal? D(string name) => row[name] is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : null;
        var created = DateTimeOffset.TryParse(S("createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTimeOffset?)null;
        var status = S("status") ?? string.Empty;
        var priced = S("priceMode") == "priced";
        return new PreOrderExportRow(
            S("code") ?? string.Empty,
            created,
            S("pharmacyName") ?? string.Empty,
            S("depotName") ?? string.Empty,
            S("repName") ?? string.Empty,
            row["lineCount"] is JsonValue n && n.TryGetValue<int>(out var count) ? count : 0,
            priced ? Money(D("gross"), S("currency"), culture) : Text("QuantityModeShort", culture),
            Money(D("localGross"), S("localCurrencyCode"), culture),
            D("effectiveDiscountPct") is { } e ? e.ToString("0.0", culture) + " %" : string.Empty,
            Statuses.Contains(status) ? Text("St_" + status, culture) : status);
    }

    private ListExportColumnSet<PreOrderExportRow> ExportColumns() => new(
    [
        new("code", c => Text("ColCode", c), (r, _) => r.Code),
        new("createdAt", c => Text("ColDate", c), (r, c) => r.CreatedAt?.ToString("g", c)),
        new("pharmacyName", c => Text("ColPharmacy", c), (r, _) => r.Pharmacy),
        new("depotName", c => Text("ColDepot", c), (r, _) => r.Depot),
        new("repName", c => Text("ColRep", c), (r, _) => r.Rep),
        new("lineCount", c => Text("ColLines", c), (r, c) => r.Lines.ToString(c)),
        new("gross", c => Text("ColTotal", c), (r, _) => r.Total),
        new("localGross", c => Text("ColLocal", c), (r, _) => r.Local),
        new("effectiveDiscountPct", c => Text("ColEffective", c), (r, _) => r.Effective),
        new("status", c => Text("ColStatus", c), (r, _) => r.Status)
    ]);

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<PreOrdersIndex>)) as IStringLocalizer<PreOrdersIndex>;
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

    // ═══ A · context, preview, the draft ═════════════════════════════════════════════════════════════════════════

    /// <summary>The "from the system 🔒" strip (company, country, currency, price mode) + the approver preview.</summary>
    [HttpGet("api/context")]
    public Task<IActionResult> Context(CancellationToken ct) => ProxyAsync(HttpMethod.Get, Orders + "/context", null, HasAny(Create), ct);

    /// <summary>The live evaluation of a draft (prices, free boxes, findings, totals, FX) — MOD-0156's, nothing is computed here.</summary>
    [HttpPost("api/preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => HasAny(Create) ? await ProxyAsync(HttpMethod.Post, Orders + "/preview", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    [HttpPost("api/orders")]
    public async Task<IActionResult> CreateDraft(CancellationToken ct)
        => HasAny(Create) ? await ProxyAsync(HttpMethod.Post, Orders, await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>One order (the abilities of the caller included): the scope is the CRM's (404 outside it).</summary>
    [HttpGet("api/orders/{id:guid}")]
    public Task<IActionResult> Order(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{Orders}/{id:D}", null, MayRead, ct);

    [HttpPut("api/orders/{id:guid}")]
    public async Task<IActionResult> UpdateDraft(Guid id, CancellationToken ct)
        => HasAny(Create) ? await ProxyAsync(HttpMethod.Put, $"{Orders}/{id:D}", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>The permission each order action needs (MOD-0168 §14) — the one table the proxy checks.</summary>
    public static readonly IReadOnlyDictionary<string, string> ActionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["discard"] = Create,
        ["signature"] = Create,
        ["copy"] = Create,
        ["submit"] = Submit,
        ["decision"] = Approve,
        ["cancel"] = Cancel,
        ["retry-transmission"] = RetryTransmission,
        ["depot-response"] = RecordDepotResponse
    };

    /// <summary>An order action: the key of <see cref="ActionKeys"/> checked HERE first; an unknown action is 404.</summary>
    // W6-FIX-a (G-5) — the segment is {verb}: "action" is MVC's reserved route value (the action name), so {action} made the
    // route never match and every order action answered 404.
    [HttpPost("api/orders/{id:guid}/{verb}")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> OrderAction(Guid id, string verb, CancellationToken ct)
    {
        if (!ActionKeys.TryGetValue(verb ?? string.Empty, out var key)) return NotFound();
        if (!HasAny(key)) return Denied();
        return await ProxyAsync(HttpMethod.Post, $"{Orders}/{id:D}/{verb}", await ReadBodyAsync(ct) ?? "{}", true, ct);
    }

    /// <summary>C — the pharmacy's last six months (the CRM admits the order's approver or read-all).</summary>
    [HttpGet("api/orders/{id:guid}/pharmacy-history")]
    public Task<IActionResult> PharmacyHistory(Guid id, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Orders}/{id:D}/pharmacy-history", null, HasAny(Approve, ReadAll), ct);

    /// <summary>
    /// E — the depot document PDF (W6-BE-c2: the one template; the depot copy carries no discount %). <paramref name="copy"/>
    /// depot (default) | pharmacy; <paramref name="download"/> = as an attachment with the CRM's file name, else inline (the
    /// viewer on screen E). The order's visibility decides (the CRM's 404).
    /// </summary>
    [HttpGet("api/orders/{id:guid}/document")]
    public async Task<IActionResult> DocumentPdf(Guid id, [FromQuery] string? copy, [FromQuery] bool download, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var which = string.IsNullOrWhiteSpace(copy) ? "depot" : copy.Trim().ToLowerInvariant();
        if (!Copies.Contains(which)) return BadRequest(new { errors = new[] { "copy must be depot or pharmacy.", "pre_order_request_invalid" } });
        using var response = await SendGatewayAsync(HttpMethod.Get, $"{Orders}/{id:D}/document?copy={which}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return download
            ? File(bytes, "application/pdf", string.IsNullOrWhiteSpace(name) ? $"pre-order-{which}.pdf" : name)
            : File(bytes, "application/pdf");
    }

    // ═══ C-link ════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The link's state for the signed-in approver: <c>valid</c> (+ the order) | <c>expired</c> | <c>consumed</c> |
    /// <c>superseded</c>; 404 when the link is unknown or not this user's (the CRM never says whose it is). The token travels
    /// in the body, not in a query string of this origin.</summary>
    [HttpPost("api/approval-link")]
    public async Task<IActionResult> ResolveLink(CancellationToken ct)
    {
        if (!HasAny(Approve)) return Denied();
        var body = await ReadBodyAsync(ct);
        string? token = null;
        try
        {
            using var doc = JsonDocument.Parse(body ?? "{}");
            token = Str(doc.RootElement, "token");
        }
        catch (JsonException)
        {
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return BadRequest(new { errors = new[] { "token is required.", "pre_order_link_unusable" } });
        return await ProxyAsync(HttpMethod.Get, $"{Links}/{Uri.EscapeDataString(token.Trim())}", null, true, ct);
    }

    // ═══ the pickers ═══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>One picker option.</summary>
    public sealed record Option(string Id, string Text, string? Sub);

    /// <summary>Pharmacies (MOD-0149 accounts of type pharmacy) by name / code — the CRM's account read.</summary>
    [HttpGet("api/lookups/pharmacies")]
    public async Task<IActionResult> Pharmacies([FromQuery] string? q, CancellationToken ct)
    {
        if (!MayRead && !HasAny(Create)) return Denied();
        return Options(await AccountsAsync(["pharmacy"], q, ct));
    }

    /// <summary>Depots (a distributor or a wholesaler, K-W13).</summary>
    [HttpGet("api/lookups/depots")]
    public async Task<IActionResult> Depots([FromQuery] string? q, CancellationToken ct)
    {
        if (!MayRead && !HasAny(Create)) return Denied();
        return Options(await AccountsAsync(DepotTypes, q, ct));
    }

    /// <summary>The products: product-master v1.1 <c>searchStockableItems</c> with <c>status=Active</c> (MOD-0168 §7: the
    /// selector; a Retired SKU is never offered, the CRM refuses it anyway) — product SKUs only (no material item).</summary>
    [HttpGet("api/lookups/skus")]
    public async Task<IActionResult> Skus([FromQuery] string? q, CancellationToken ct)
    {
        if (!MayRead && !HasAny(Create)) return Denied();
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim()[..Math.Min(q.Trim().Length, 200)]);
        var data = await ReadDataAsync("/api/product-master/stockable-items?status=Active&page=1&pageSize=25" + search, ct);
        if (data is not { ValueKind: JsonValueKind.Object } page || !page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Options(null);
        }

        return Options(SkuOptionsOf(items));
    }

    /// <summary>Static, so MVC never treats it as an action: the stockable rows of a product (Lsku / FinishedGood / Gsku) as
    /// options — name, then code · level.</summary>
    public static List<Option> SkuOptionsOf(JsonElement items)
    {
        var options = new List<Option>();
        foreach (var item in items.EnumerateArray())
        {
            var level = Str(item, "skuLevel");
            if (string.Equals(level, "Item", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Guid.TryParse(Str(item, "skuId"), out var id)) continue;
            var name = Str(item, "name") ?? Str(item, "canonicalCode") ?? id.ToString("D");
            options.Add(new Option(id.ToString("D"), name, string.Join(" · ", new[] { Str(item, "canonicalCode"), level }.Where(x => !string.IsNullOrWhiteSpace(x)))));
        }

        return options;
    }

    private async Task<List<Option>?> AccountsAsync(IEnumerable<string> types, string? q, CancellationToken ct)
    {
        var options = new List<Option>();
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&search=" + Uri.EscapeDataString(q.Trim());
        var answered = false;
        foreach (var type in types)
        {
            var data = await ReadDataAsync($"/api/crm/accounts?accountType={type}&page=1&pageSize=20{search}", ct);
            if (data is not { } d) continue;
            answered = true;
            var items = d.ValueKind == JsonValueKind.Object && d.TryGetProperty("items", out var i) ? i : d;
            if (items.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in items.EnumerateArray())
            {
                if (!Guid.TryParse(Str(item, "id"), out var id) || options.Any(o => o.Id == id.ToString("D"))) continue;
                options.Add(new Option(id.ToString("D"), Str(item, "accountName") ?? Str(item, "accountCode") ?? id.ToString("D"), Str(item, "accountCode")));
            }
        }

        return answered ? options : null;
    }

    private IActionResult Options(List<Option>? options)
        => Ok(new { data = new { available = options is not null, items = (IEnumerable<Option>?)options ?? [] } });
}
