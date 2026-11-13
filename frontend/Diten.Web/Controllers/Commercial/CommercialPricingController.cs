using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.Commercial;
using Diten.Web.Views.Commercial.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-b) — <c>/Commercial/Pricing</c> (manifest page <c>COMMERCIALPRICING</c>, one menu item): screen D of
/// MOD-0156 on five tabs of the shared tab card, each its own page — price lists (list, details with the difference to the
/// previous version, manual create / edit — EK-1, the three-step import, submit / decision), MF / discount rules (list,
/// details with the versions, create / edit, revise / retire, submit / decision), the rule trial, the change history and
/// the FX rates. Every call is a same-origin proxy to the W6-BE-b endpoints with the caller's session; the CRM computes
/// every MF ratio, combined discount and outcome (the page shows them — AC-EVAL-3) and checks every key and the four-eyes
/// rule again. The keys are checked HERE too, before the CRM is asked.
/// <para>
/// FOUR EYES (D13): the proxy marks a list / rule the caller prepared (<c>mine</c>: created, last edited or submitted it —
/// the CRM's <c>WorkflowApprovalRules.IsOwn</c> on the same identity claim order) so the page never draws its decision
/// buttons to the preparer; the CRM refuses that decision with 403 anyway.
/// </para>
/// </summary>
[Authorize]
[Route("Commercial/Pricing")]
public sealed class CommercialPricingController : CrmGatewayProxy
{
    public const string ListsRead = "commercial.price-lists.read";
    public const string ListsCreate = "commercial.price-lists.create";
    public const string ListsUpdate = "commercial.price-lists.update";
    public const string ListsImport = "commercial.price-lists.import";
    public const string ListsSubmit = "commercial.price-lists.submit";
    public const string ListsPublish = "commercial.price-lists.publish";
    public const string ListsExport = "commercial.price-lists.export";
    public const string RulesRead = "commercial.pricing-rules.read";
    public const string RulesCreate = "commercial.pricing-rules.create";
    public const string RulesUpdate = "commercial.pricing-rules.update";
    public const string RulesSubmit = "commercial.pricing-rules.submit";
    public const string RulesPublish = "commercial.pricing-rules.publish";
    public const string FxRead = "commercial.fx-rates.read";
    public const string FxCreate = "commercial.fx-rates.create";
    public const string FxUpdate = "commercial.fx-rates.update";

    /// <summary>Every MOD-0156 §14 key this screen uses (fx-rates.import has no screen yet).</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        ListsRead, ListsCreate, ListsUpdate, ListsImport, ListsSubmit, ListsPublish, ListsExport, RulesRead, RulesCreate, RulesUpdate, RulesSubmit,
        RulesPublish, FxRead, FxCreate, FxUpdate
    ];

    /// <summary>The CRM's page cap (PricingLimits.MaxPageSize).</summary>
    public const int MaxPageSize = 200;

    public const int ExportMax = 5000;

    public static readonly IReadOnlyList<string> ListStatuses = ["draft", "in_review", "published", "discarded"];
    public static readonly IReadOnlyList<string> ListBadges = ["draft", "in_review", "planned", "in-force", "past", "discarded"];
    public static readonly IReadOnlyList<string> Kinds = ["official", "company"];
    public static readonly IReadOnlyList<string> RuleStatuses = ["draft", "in_review", "active", "superseded", "retired", "discarded"];
    public static readonly IReadOnlyList<string> HistoryTypes = ["price-list", "price-list-item", "pricing-rule", "fx-rate"];

    private const string Lists = "/api/commercial/price-lists";
    private const string Imports = "/api/commercial/price-list-imports";
    private const string Rules = "/api/commercial/pricing-rules";
    private const string Fx = "/api/commercial/fx-rates";
    private const string FilterInvalid = "pricing_filter_invalid";

    public CommercialPricingController(HttpClient httpClient, IConfiguration configuration, ILogger<CommercialPricingController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    /// <summary>Static, so MVC never treats it as an action: the flags of one page from the caller's keys.</summary>
    public static PricingPageViewModel Flags(Func<string, bool> has, string tab, string screen, Guid? id = null)
    {
        var m = new PricingPageViewModel
        {
            CanReadLists = has(ListsRead), CanCreateList = has(ListsCreate), CanUpdateList = has(ListsUpdate), CanImportList = has(ListsImport),
            CanSubmitList = has(ListsSubmit), CanPublishList = has(ListsPublish), CanExportLists = has(ListsExport),
            CanReadRules = has(RulesRead), CanCreateRule = has(RulesCreate), CanUpdateRule = has(RulesUpdate), CanSubmitRule = has(RulesSubmit),
            CanPublishRule = has(RulesPublish), CanReadFx = has(FxRead), CanCreateFx = has(FxCreate), CanUpdateFx = has(FxUpdate),
            Tab = tab, Screen = screen, Id = id
        };
        m.Allowed = (tab, screen) switch
        {
            ("lists", "index") or ("lists", "details") => m.CanReadLists,
            ("lists", "create") => m.CanCreateList,
            ("lists", "edit") => m.CanUpdateList,
            ("lists", "import") => m.CanImportList,
            ("rules", "index") or ("rules", "details") or ("trial", _) => m.CanReadRules,
            ("rules", "create") => m.CanCreateRule,
            ("rules", "edit") => m.CanUpdateRule,
            ("history", _) => m.CanReadHistory,
            ("fx", "index") => m.CanReadFx,
            ("fx", "create") => m.CanCreateFx,
            ("fx", "edit") => m.CanUpdateFx,
            _ => false
        };
        return m;
    }

    private IActionResult Page(string view, string tab, string screen, Guid? id = null)
        => View("~/Views/Commercial/" + view, Flags(key => HasAny(key), tab, screen, id));

    // ═══ the pages (each draws only the access notice when its flag is false — UAS-001) ═══════════════════════════

    [HttpGet("")]
    public IActionResult Index() => Page("PriceLists/Index.cshtml", "lists", "index");

    [HttpGet("PriceLists")]
    public IActionResult PriceLists() => Page("PriceLists/Index.cshtml", "lists", "index");

    [HttpGet("PriceLists/Create")]
    public IActionResult PriceListCreate() => Page("PriceLists/Create.cshtml", "lists", "create");

    [HttpGet("PriceLists/Edit/{id:guid}")]
    public IActionResult PriceListEdit(Guid id) => Page("PriceLists/Edit.cshtml", "lists", "edit", id);

    [HttpGet("PriceLists/Details/{id:guid}")]
    public IActionResult PriceListDetails(Guid id) => Page("PriceLists/Details.cshtml", "lists", "details", id);

    [HttpGet("PriceLists/Import")]
    public IActionResult PriceListImport([FromQuery] Guid? id) => Page("PriceLists/Import.cshtml", "lists", "import", id);

    [HttpGet("Rules")]
    public IActionResult RulesPage() => Page("PricingRules/Index.cshtml", "rules", "index");

    [HttpGet("Rules/Create")]
    public IActionResult RuleCreate() => Page("PricingRules/Create.cshtml", "rules", "create");

    [HttpGet("Rules/Edit/{id:guid}")]
    public IActionResult RuleEdit(Guid id) => Page("PricingRules/Edit.cshtml", "rules", "edit", id);

    [HttpGet("Rules/Details/{id:guid}")]
    public IActionResult RuleDetails(Guid id) => Page("PricingRules/Details.cshtml", "rules", "details", id);

    [HttpGet("Trial")]
    public IActionResult Trial() => Page("Pricing/Trial.cshtml", "trial", "index");

    [HttpGet("History")]
    public IActionResult History() => Page("PricingHistory/Index.cshtml", "history", "index");

    [HttpGet("FxRates")]
    public IActionResult FxRates() => Page("FxRates/Index.cshtml", "fx", "index");

    [HttpGet("FxRates/Create")]
    public IActionResult FxCreatePage() => Page("FxRates/Create.cshtml", "fx", "create");

    [HttpGet("FxRates/Edit/{id:guid}")]
    public IActionResult FxEditPage(Guid id) => Page("FxRates/Edit.cshtml", "fx", "edit", id);

    // ═══ four eyes ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The caller's identity as the CRM records it (HttpActorContext: sub, name identifier, e-mail, name).</summary>
    public static string? ActorOf(ClaimsPrincipal user)
        => new[] { user.FindFirstValue("sub"), user.FindFirstValue(ClaimTypes.NameIdentifier), user.FindFirstValue(ClaimTypes.Email), user.FindFirstValue("email"), user.Identity?.Name }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Static, so MVC never treats it as an action: the caller prepared this list / rule (created it, edited it last
    /// or submitted its open round) — the twin of the CRM's <c>WorkflowApprovalRules.IsOwn</c>.</summary>
    public static bool IsMine(JsonObject record, string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) return false;
        static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        bool Same(string? x) => !string.IsNullOrWhiteSpace(x) && string.Equals(x.Trim(), actor.Trim(), StringComparison.OrdinalIgnoreCase);
        var rounds = record["rounds"] as JsonArray;
        var round = rounds?.OfType<JsonObject>().LastOrDefault(r => r["outcome"] is null) ?? rounds?.OfType<JsonObject>().LastOrDefault();
        return Same(S(record["createdBy"])) || Same(S(record["updatedBy"])) || Same(S(round?["submittedBy"]));
    }

    /// <summary>A CRM answer passed on with <c>mine</c> set on each list / rule it carries (a page's items, a record, a rule
    /// detail's rule and versions).</summary>
    private async Task<IActionResult> WithMineAsync(string path, bool allowed, CancellationToken ct)
    {
        if (!allowed) return Denied();
        using var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (JsonException)
        {
            return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        }

        var data = root is JsonObject o && o["data"] is JsonObject d ? d : root as JsonObject;
        if (data is null) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        Mark(data, ActorOf(User));
        return Ok(new { data });
    }

    /// <summary>Static, so MVC never treats it as an action: sets <c>mine</c> on a record, a page's items, a rule detail.</summary>
    public static void Mark(JsonObject data, string? actor)
    {
        if (data["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>()) item["mine"] = IsMine(item, actor);
        }

        if (data["rule"] is JsonObject rule) rule["mine"] = IsMine(rule, actor);
        if (data["versions"] is JsonArray versions)
        {
            foreach (var v in versions.OfType<JsonObject>()) v["mine"] = IsMine(v, actor);
        }

        if (data["status"] is not null) data["mine"] = IsMine(data, actor);
    }

    // ═══ the lists (server mode) ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>Static, so MVC never treats it as an action: a query string of the non-empty values (escaped) + the page.</summary>
    public static string Query(string path, IEnumerable<(string Name, string? Value)> values, int page, int pageSize)
    {
        var q = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(v => v.Name + "=" + Uri.EscapeDataString(v.Value!.Trim())).ToList();
        q.Add("page=" + Math.Max(1, page));
        q.Add("pageSize=" + Math.Clamp(pageSize, 1, MaxPageSize));
        return path + "?" + string.Join("&", q);
    }

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? Refusal(int? length, string? orderBy, IEnumerable<(string Name, string? Value, IReadOnlyList<string>? Allowed, bool Id)> filters)
    {
        if (length is { } n && (n < 1 || n > 100)) return "length must be 1–100.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list is ordered by the service; orderBy is not accepted.";
        foreach (var (name, value, allowed, id) in filters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (value == "invalid") return name + " must be a day (yyyy-MM-dd).";
            if (id && !Guid.TryParse(value.Trim(), out _)) return name + " must be an id.";
            if (allowed is not null && !allowed.Contains(value.Trim())) return name + " must be one of: " + string.Join(", ", allowed) + ".";
        }

        return null;
    }

    private async Task<IActionResult> PageAsync(string path, int? start, int? length, string? orderBy,
        IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Allowed, bool Id)> filters, bool allowed, CancellationToken ct)
    {
        if (!allowed) return Denied();
        if (Refusal(length, orderBy, filters) is { } refusal) return BadRequest(new { errors = new[] { refusal, FilterInvalid } });
        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        var query = Query(path, filters.Select(f => (f.Name, f.Value)), page, size);
        var result = await WithMineAsync(query, true, ct);
        if (result is not OkObjectResult ok) return result;
        var data = JsonSerializer.SerializeToNode(ok.Value)!["data"]!.AsObject();
        var total = data["total"] is JsonValue t && t.TryGetValue<long>(out var x) ? x : 0;
        var all = total;
        if (filters.Any(f => !string.IsNullOrWhiteSpace(f.Value)))
        {
            using var whole = await SendGatewayAsync(HttpMethod.Get, Query(path, [], 1, 1), null, ct);
            if (whole is { IsSuccessStatusCode: true })
            {
                try
                {
                    var w = JsonNode.Parse(await whole.Content.ReadAsStringAsync(ct));
                    var wd = w is JsonObject wo && wo["data"] is JsonObject wdd ? wdd : w as JsonObject;
                    if (wd?["total"] is JsonValue wt && wt.TryGetValue<long>(out var wn)) all = wn;
                }
                catch (JsonException)
                {
                }
            }
        }

        return Ok(new { data = new { items = data["items"], total = all, filteredTotal = total } });
    }

    [HttpGet("api/price-lists")]
    public Task<IActionResult> PriceListPage([FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy,
        [FromQuery] string? legalEntity, [FromQuery] string? country, [FromQuery] string? currency, [FromQuery] string? kind, [FromQuery] string? status,
        CancellationToken ct)
        => PageAsync(Lists, start, length, orderBy,
        [
            ("legalEntityId", legalEntity, null, true), ("countryCode", country?.ToUpperInvariant(), null, false), ("currency", currency?.ToUpperInvariant(), null, false),
            ("kind", kind, Kinds, false), ("status", status, ListBadges, false), ("search", search, null, false)
        ], HasAny(ListsRead), ct);

    [HttpGet("api/rules")]
    public Task<IActionResult> RulePage([FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy,
        [FromQuery] string? legalEntity, [FromQuery] string? sku, [FromQuery] string? status, CancellationToken ct)
        => PageAsync(Rules, start, length, orderBy,
            [("legalEntityId", legalEntity, null, true), ("skuId", sku, null, true), ("status", status, RuleStatuses, false), ("search", search, null, false)],
            HasAny(RulesRead), ct);

    [HttpGet("api/fx-rates")]
    public Task<IActionResult> FxPage([FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? orderBy, [FromQuery] string? legalEntity,
        [FromQuery] string? fromCurrency, [FromQuery] string? toCurrency, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
        => PageAsync(Fx, start, length, orderBy,
        [
            ("legalEntityId", legalEntity, null, true), ("fromCurrency", fromCurrency?.ToUpperInvariant(), null, false),
            ("toCurrency", toCurrency?.ToUpperInvariant(), null, false), ("from", Day(from), null, false), ("to", Day(to), null, false)
        ], HasAny(FxRead), ct);

    [HttpGet("api/history")]
    public Task<IActionResult> HistoryPage([FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? orderBy, [FromQuery] string? entityType,
        [FromQuery] string? entityId, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
        => PageAsync("/api/commercial/pricing-history", start, length, orderBy,
            [("entityType", entityType, HistoryTypes, false), ("entityId", entityId, null, true), ("from", Day(from), null, false), ("to", Day(to), null, false)],
            HasAny(ListsRead, RulesRead, FxRead), ct);

    private static string? Day(string? v)
        => string.IsNullOrWhiteSpace(v) ? null : DateOnly.TryParseExact(v.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "invalid";

    /// <summary>The module dictionary (statuses, tiers, kinds, reason codes, FX sources, segments switch).</summary>
    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) => ProxyAsync(HttpMethod.Get, "/api/commercial/pricing-contract", null, HasAny(ListsRead, RulesRead), ct);

    // ═══ price lists ═══════════════════════════════════════════════════════════════════════════════════════════

    [HttpPost("api/price-lists")]
    public async Task<IActionResult> CreateList(CancellationToken ct)
        => HasAny(ListsCreate) ? await ProxyAsync(HttpMethod.Post, Lists, await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    [HttpGet("api/price-lists/{id:guid}")]
    public Task<IActionResult> List(Guid id, CancellationToken ct) => WithMineAsync($"{Lists}/{id:D}", HasAny(ListsRead), ct);

    [HttpPut("api/price-lists/{id:guid}")]
    public async Task<IActionResult> UpdateList(Guid id, CancellationToken ct)
        => HasAny(ListsUpdate) ? await ProxyAsync(HttpMethod.Put, $"{Lists}/{id:D}", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>The items of a list (paged; <paramref name="diff"/> = previous: each item's difference + the removed ones).</summary>
    [HttpGet("api/price-lists/{id:guid}/items")]
    public Task<IActionResult> Items(Guid id, [FromQuery] string? search, [FromQuery] string? diff, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(diff) && diff.Trim() != "previous") return Task.FromResult<IActionResult>(BadRequest(new { errors = new[] { "diff must be previous.", FilterInvalid } }));
        return ProxyAsync(HttpMethod.Get, Query($"{Lists}/{id:D}/items", [("search", search), ("diff", diff)], page ?? 1, pageSize ?? 50), null, HasAny(ListsRead), ct);
    }

    /// <summary>The permission each list action needs (MOD-0156 §14) — the one table the proxy checks.</summary>
    public static readonly IReadOnlyDictionary<string, string> ListActionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["versions"] = ListsCreate, ["discard"] = ListsCreate, ["submit"] = ListsSubmit, ["decision"] = ListsPublish
    };

    // W6-FIX-a (G-5) — {verb}, not {action}: "action" is MVC's reserved route value, so the three action routes below 404ed.
    [HttpPost("api/price-lists/{id:guid}/{verb}")]
    public async Task<IActionResult> ListAction(Guid id, string verb, CancellationToken ct)
    {
        if (!ListActionKeys.TryGetValue(verb ?? string.Empty, out var key)) return NotFound();
        if (!HasAny(key)) return Denied();
        return await ProxyAsync(HttpMethod.Post, $"{Lists}/{id:D}/{verb}", await ReadBodyAsync(ct) ?? "{}", true, ct);
    }

    // ═══ the import (3 steps: file → preview → convert to a draft, then the draft's submit) ═══════════════════════

    [HttpPost("api/imports")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public Task<IActionResult> Upload(CancellationToken ct) => ProxyMultipartAsync(Imports, HasAny(ListsImport), ct);

    /// <summary>The preview: the counts (matched · unmatched · retired · duplicate · invalid price) and the difference, + one
    /// page of rows (<paramref name="rowStatus"/> / <paramref name="diffKind"/> narrow it).</summary>
    [HttpGet("api/imports/{id:guid}")]
    public Task<IActionResult> Import(Guid id, [FromQuery] string? rowStatus, [FromQuery] string? diffKind, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, Query($"{Imports}/{id:D}", [("rowStatus", rowStatus), ("diffKind", diffKind)], page ?? 1, pageSize ?? 50), null, HasAny(ListsImport), ct);

    public static readonly IReadOnlyList<string> ImportActions = ["convert", "discard"];

    [HttpPost("api/imports/{id:guid}/{verb}")]
    public async Task<IActionResult> ImportAction(Guid id, string verb, CancellationToken ct)
    {
        if (!ImportActions.Contains(verb ?? string.Empty)) return NotFound();
        return await ProxyAsync(HttpMethod.Post, $"{Imports}/{id:D}/{verb}", "{}", HasAny(ListsImport), ct);
    }

    // ═══ rules ═════════════════════════════════════════════════════════════════════════════════════════════════

    [HttpPost("api/rules")]
    public async Task<IActionResult> CreateRule(CancellationToken ct)
        => HasAny(RulesCreate) ? await ProxyAsync(HttpMethod.Post, Rules, await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>Kural deneme — the CRM's evaluation of the typed lines (a read on POST; nothing saved). The rule form asks it
    /// for the MF ratio beside "10+2" too: no formula on the page.</summary>
    [HttpPost("api/rules/trial")]
    public async Task<IActionResult> RuleTrial(CancellationToken ct)
        => HasAny(RulesRead) ? await ProxyAsync(HttpMethod.Post, Rules + "/trial", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    [HttpGet("api/rules/{id:guid}")]
    public Task<IActionResult> Rule(Guid id, CancellationToken ct) => WithMineAsync($"{Rules}/{id:D}", HasAny(RulesRead), ct);

    [HttpPut("api/rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, CancellationToken ct)
        => HasAny(RulesUpdate) ? await ProxyAsync(HttpMethod.Put, $"{Rules}/{id:D}", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    public static readonly IReadOnlyDictionary<string, string> RuleActionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["revisions"] = RulesUpdate, ["discard"] = RulesUpdate, ["submit"] = RulesSubmit, ["decision"] = RulesPublish
    };

    [HttpPost("api/rules/{id:guid}/{verb}")]
    public async Task<IActionResult> RuleAction(Guid id, string verb, CancellationToken ct)
    {
        if (!RuleActionKeys.TryGetValue(verb ?? string.Empty, out var key)) return NotFound();
        if (!HasAny(key)) return Denied();
        return await ProxyAsync(HttpMethod.Post, $"{Rules}/{id:D}/{verb}", await ReadBodyAsync(ct) ?? "{}", true, ct);
    }

    // ═══ FX rates ══════════════════════════════════════════════════════════════════════════════════════════════

    [HttpPost("api/fx-rates")]
    public async Task<IActionResult> CreateFx(CancellationToken ct)
        => HasAny(FxCreate) ? await ProxyAsync(HttpMethod.Post, Fx, await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>
    /// One rate for the correction form. The CRM has no single-rate read: its list is scanned page by page (200 rows a
    /// page, at most <see cref="FxScanPages"/> pages) for the id — 404 when it is not there.
    /// </summary>
    [HttpGet("api/fx-rates/{id:guid}")]
    public async Task<IActionResult> FxRate(Guid id, CancellationToken ct)
    {
        if (!HasAny(FxRead)) return Denied();
        for (var page = 1; page <= FxScanPages; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, Query(Fx, [], page, MaxPageSize), null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            var data = root is JsonObject o && o["data"] is JsonObject d ? d : root as JsonObject;
            var items = (data?["items"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            var hit = items.FirstOrDefault(x => Guid.TryParse(x["id"]?.GetValue<string>(), out var g) && g == id);
            if (hit is not null) return Ok(new { data = hit.DeepClone() });
            if (items.Count < MaxPageSize) break;
        }

        return NotFound(new { errors = new[] { "The rate was not found.", "pricing_not_found" } });
    }

    /// <summary>How many pages of the rate list a single-rate read scans.</summary>
    public const int FxScanPages = 10;

    /// <summary>A correction: the CRM writes the old → new into the change history (AC-FX-2).</summary>
    [HttpPut("api/fx-rates/{id:guid}")]
    public async Task<IActionResult> UpdateFx(Guid id, CancellationToken ct)
        => HasAny(FxUpdate) ? await ProxyAsync(HttpMethod.Put, $"{Fx}/{id:D}", await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    // ═══ the export of the price lists (BL-452: the file is the screen) ════════════════════════════════════════

    [HttpGet("api/price-lists/export")]
    public async Task<IActionResult> ExportLists([FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] string? search,
        [FromQuery] string? orderBy, [FromQuery] string? legalEntity, [FromQuery] string? country, [FromQuery] string? currency, [FromQuery] string? kind,
        [FromQuery] string? status, CancellationToken ct)
    {
        if (!HasAny(ListsRead) || !HasAny(ListsExport)) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        if (!ExportColumns().TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        IReadOnlyList<(string Name, string? Value, IReadOnlyList<string>? Allowed, bool Id)> filters =
        [
            ("legalEntityId", legalEntity, null, true), ("countryCode", country?.ToUpperInvariant(), null, false), ("currency", currency?.ToUpperInvariant(), null, false),
            ("kind", kind, Kinds, false), ("status", status, ListBadges, false), ("search", search, null, false)
        ];
        if (Refusal(null, orderBy, filters) is { } refusal) return ExportRefusal(400, refusal, FilterInvalid);
        var rows = new List<JsonObject>();
        for (var page = 1; ; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, Query(Lists, filters.Select(f => (f.Name, f.Value)), page, MaxPageSize), null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            var data = root is JsonObject o && o["data"] is JsonObject d ? d : root as JsonObject;
            var chunk = (data?["items"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            var total = data?["total"] is JsonValue t && t.TryGetValue<long>(out var n) ? n : chunk.Count;
            if (total > ExportMax) return ExportRefusal(StatusCodes.Status413PayloadTooLarge, $"{total} lists match; an export carries at most {ExportMax}.", ListExportContract.TooLargeCode);
            rows.AddRange(chunk);
            if (chunk.Count < MaxPageSize || rows.Count >= total) break;
        }

        var content = ListExportWriter.Write(fileFormat, exportColumns, rows.Select(r => RowOf(r, culture)).ToList(), culture, Text("PageTitle", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("price-lists", DateTimeOffset.UtcNow, fileFormat));
    }

    public sealed record PriceListExportRow(string Name, string Company, string Country, string Currency, string Kind, string Validity, string Status, int Items);

    private PriceListExportRow RowOf(JsonObject r, CultureInfo culture)
    {
        string? S(string n) => r[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var badge = S("badge") ?? string.Empty;
        var kind = S("kind") ?? string.Empty;
        return new PriceListExportRow(S("name") ?? string.Empty, S("legalEntityName") ?? S("legalEntityCode") ?? string.Empty, S("countryCode") ?? string.Empty,
            S("currency") ?? string.Empty, Kinds.Contains(kind) ? Text("Kind_" + kind, culture) : kind,
            (S("validFrom") ?? string.Empty) + " – " + (S("effectiveValidTo") ?? S("validTo") ?? string.Empty),
            ListBadges.Contains(badge) ? Text("Badge_" + badge.Replace('-', '_'), culture) : badge,
            r["itemCount"] is JsonValue c && c.TryGetValue<int>(out var i) ? i : 0);
    }

    private ListExportColumnSet<PriceListExportRow> ExportColumns() => new(
    [
        new("name", c => Text("ColName", c), (r, _) => r.Name),
        new("legalEntityName", c => Text("ColCompany", c), (r, _) => r.Company),
        new("countryCode", c => Text("ColCountry", c), (r, _) => r.Country),
        new("currency", c => Text("ColCurrency", c), (r, _) => r.Currency),
        new("kind", c => Text("ColKind", c), (r, _) => r.Kind),
        new("validFrom", c => Text("ColValidity", c), (r, _) => r.Validity),
        new("badge", c => Text("ColStatus", c), (r, _) => r.Status),
        new("itemCount", c => Text("ColItems", c), (r, c) => r.Items.ToString(c))
    ]);

    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<PricingIndex>)) as IStringLocalizer<PricingIndex>;
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

    private ObjectResult ExportRefusal(int statusCode, string message, string code)
        => StatusCode(statusCode, new { isSuccessful = false, statusCode, errors = new[] { message }, errorCodes = new[] { new { code } } });

    // ═══ the pickers ═══════════════════════════════════════════════════════════════════════════════════════════

    public sealed record Option(string Id, string Text, string? Sub);

    /// <summary>The selling companies (MDM referenceable legal entities) with their country.</summary>
    [HttpGet("api/lookups/legal-entities")]
    public async Task<IActionResult> LegalEntities(CancellationToken ct)
    {
        if (!HasAny(Keys.ToArray())) return Denied();
        using var response = await SendGatewayAsync(HttpMethod.Get, "/api/legal-entities/lookup", null, ct);
        var items = response is { IsSuccessStatusCode: true } ? PreOrderSettingsController.CompaniesOf(await response.Content.ReadAsStringAsync(ct)) : null;
        return Ok(new { data = new { available = items is not null, items = items?.Select(c => new Option(c.Id, c.Name, c.CountryCode)) ?? [] } });
    }

    /// <summary>The products: product-master v1.1 <c>searchStockableItems?status=Active</c> (a Retired SKU is never offered; the
    /// CRM refuses it anyway) — product SKUs only.</summary>
    [HttpGet("api/lookups/skus")]
    public async Task<IActionResult> Skus([FromQuery] string? q, CancellationToken ct)
    {
        if (!HasAny(ListsCreate, ListsUpdate, RulesRead, RulesCreate, RulesUpdate)) return Denied();
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim()[..Math.Min(q.Trim().Length, 200)]);
        var data = await ReadDataAsync("/api/product-master/stockable-items?status=Active&page=1&pageSize=25" + search, ct);
        var items = data is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array
            ? PreOrdersController.SkuOptionsOf(i).Select(o => new Option(o.Id, o.Text, o.Sub)).ToList()
            : null;
        return Ok(new { data = new { available = items is not null, items = (IEnumerable<Option>?)items ?? [] } });
    }
}
