using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-d) — the visit evidence settings page: the tenant's "Start the visit" rules (form + preview + save
/// confirm), a rep's effective rules, the change history and the time-boxed exceptions approved in the Work Center. All
/// business traffic is proxied server-side through the Gateway; the browser never sees a service URL or a bearer token,
/// and the CrmService stays the authoritative permission + validation layer.
/// <para>Every proxy requires the keys of its CRM endpoint: the policy reads (policy, history, effective, reps)
/// <c>crm.visit-evidence-policy.read</c> OR <c>.manage</c>; the policy PUT <c>.manage</c>; the exception reads and the
/// reviewer's decision <c>crm.visit-evidence-exception.read</c>; every other exception write <c>.manage</c>.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitEvidenceSettings")]
public sealed class VisitEvidenceSettingsController : Controller
{
    public const string PolicyRead = "crm.visit-evidence-policy.read";
    public const string PolicyManage = "crm.visit-evidence-policy.manage";
    public const string ExceptionRead = "crm.visit-evidence-exception.read";
    public const string ExceptionManage = "crm.visit-evidence-exception.manage";

    private const string ViewRoot = "~/Views/CRM/VisitEvidenceSettings";
    private const string Policy = "/api/crm/visit-evidence-policy";
    private const string Exceptions = "/api/crm/visit-evidence-exceptions";

    /// <summary>WP-VW-W3L — the MOD-0048 set a business unit layer's code is checked against (the territory model's set).</summary>
    private const string BusinessUnitSetCode = "business-unit";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitEvidenceSettingsController> _logger;
    private readonly Diten.Web.Services.CrmReferenceSetReader _referenceSets;

    public VisitEvidenceSettingsController(
        HttpClient httpClient, IConfiguration configuration, ILogger<VisitEvidenceSettingsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
        _referenceSets = new Diten.Web.Services.CrmReferenceSetReader(httpClient, _gatewayUrl, logger);
    }

    // ---------------- page ----------------

    /// <summary>The page. Without a policy read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index()
        => View($"{ViewRoot}/Index.cshtml", new VisitEvidenceSettingsIndexViewModel
        {
            CanRead = HasAny(PolicyRead, PolicyManage),
            CanManagePolicy = HasAny(PolicyManage),
            CanReadExceptions = HasAny(ExceptionRead),
            CanManageExceptions = HasAny(ExceptionManage),
            CanReadQrCodes = HasAny("crm.visit-qr.read", "crm.visit-qr.manage"), // W3E — the "manage institution QR codes" link
            Me = KnowledgePathStudioReview.ActorOf(User)
        });

    // ---------------- the tenant policy ----------------

    [HttpGet("api/policy")]
    public Task<IActionResult> GetPolicy(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, Policy, null, ct, PolicyRead, PolicyManage);

    [HttpPut("api/policy")]
    public Task<IActionResult> PutPolicy(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Put, Policy, ct, PolicyManage);

    /// <summary>The list contract's page cap (the Golden page sizes 10 / 25 / 50 / 100).</summary>
    public const int MaxListPageSize = 100;

    /// <summary>
    /// W3E-E4R2-FIX-d (U-7) — the change history as a Golden Compact SERVER-mode list: the page asks with the list contract
    /// (start · length · draw; no search, no sort — the CRM pages the append-only history newest first) and reads
    /// { items, total, filteredTotal, displayNames }. The CRM keeps ITS contract (page / pageSize); this proxy translates and
    /// nothing else. Refusals before any CRM call: a page size outside 1–100, a search, a sort key.
    /// </summary>
    [HttpGet("api/policy/history")]
    public async Task<IActionResult> History(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir, CancellationToken ct)
    {
        if (!HasAny(PolicyRead, PolicyManage)) return Denied();
        var refusal = ListRefusal(length, orderBy, orderDir) ?? (string.IsNullOrWhiteSpace(search) ? null : "The history has no search.");
        if (refusal is not null) return Refused(refusal);
        // WP-VW-W3L — the scope filter (?scope=&scopeRef=): one scope's changes, or every scope's when absent
        if (HistoryScopeQuery(Request.Query["scope"], Request.Query["scopeRef"]) is not { } scopeQuery) return Refused("scope must be tenant | legal-entity | business-unit (with its scopeRef).");
        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        using var response = await SendGatewayAsync(HttpMethod.Get, $"{Policy}/history?page={page}&pageSize={size}{scopeQuery}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        if (await ListPageOf(response, ct) is not { } chunk) return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        return Ok(new { data = new { items = chunk.Items, total = chunk.Total, filteredTotal = chunk.Total, displayNames = chunk.Names } });
    }

    /// <summary>
    /// W3E-E4R2-FIX-d (U-7) — the REP ROSTER as a Golden Compact SERVER-mode list: start · length · search → the CRM's paged
    /// roster (page · pageSize · q, ordered by name then id). <c>total</c> is the tenant's whole roster (a second, one-row read
    /// only when a search is applied), <c>filteredTotal</c> what matched — never a silently cut list (the picker read keeps its
    /// 20 for the pickers only). Refusals before any CRM call: a page size outside 1–100, a sort key.
    /// </summary>
    [HttpGet("api/policy/reps/roster")]
    public async Task<IActionResult> Roster(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir, CancellationToken ct)
    {
        if (!HasAny(PolicyRead, PolicyManage)) return Denied();
        if (ListRefusal(length, orderBy, orderDir) is { } refusal) return Refused(refusal);
        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        using var response = await SendGatewayAsync(HttpMethod.Get, RosterPath(search, page, size), null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        if (await ListPageOf(response, ct) is not { } matched) return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        var total = matched.Total;
        if (!string.IsNullOrWhiteSpace(search))
        {
            using var all = await SendGatewayAsync(HttpMethod.Get, RosterPath(null, 1, 1), null, ct);
            total = all is { IsSuccessStatusCode: true } && await ListPageOf(all, ct) is { } whole ? whole.Total : matched.Total;
        }

        return Ok(new { data = new { items = matched.Items, total, filteredTotal = matched.Total } });
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM roster path (q escaped and trimmed, left out when empty).</summary>
    public static string RosterPath(string? q, int page, int pageSize)
        => $"{Policy}/reps/roster?" + (string.IsNullOrWhiteSpace(q) ? string.Empty : "q=" + Uri.EscapeDataString(q.Trim()) + "&")
           + $"page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, MaxListPageSize)}";

    /// <summary>Static, so MVC never treats it as an action: the list contract's refusals (null = acceptable).</summary>
    public static string? ListRefusal(int? length, string? orderBy, string? orderDir)
    {
        if (length is { } n && (n < 1 || n > MaxListPageSize)) return $"length must be 1–{MaxListPageSize}.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The list keeps the CRM's order; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        return null;
    }

    private sealed record ListPage(JsonElement Items, long Total, JsonElement? Names);

    private static async Task<ListPage?> ListPageOf(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;
            var total = data.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var n) ? n : items.GetArrayLength();
            JsonElement? names = data.TryGetProperty("displayNames", out var dn) && dn.ValueKind == JsonValueKind.Object ? dn.Clone() : null;
            return new ListPage(items.Clone(), total, names);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------------- the history export (BL-452: the file is the screen) ----------------

    /// <summary>The fields whose value is a number (with its unit) or a switch — the rest are choices (Opt_{field}_{value}).</summary>
    private static readonly IReadOnlyDictionary<string, string> NumberUnits = new Dictionary<string, string>
    {
        ["radiusMeters"] = "UnitMeters", ["maxAccuracyMeters"] = "UnitMeters", ["shortVisitSeconds"] = "UnitSeconds"
    };

    private static readonly IReadOnlySet<string> Switches = new HashSet<string> { "doctorConfirmation", "durationConsistency" };

    /// <summary>
    /// W3E-E4R2-FIX-d — <c>GET api/policy/history/export?format=csv|xlsx&amp;columns=…</c>: EVERY change (the CRM history read
    /// page by page, 200 at a time), the visible columns in screen order, the person by name and the field / values in the
    /// reader's language (the page's own words). More than 50 000 → 413 EXPORT_TOO_LARGE before a row is written; an
    /// unknown column → 400.
    /// </summary>
    [HttpGet("api/policy/history/export")]
    public async Task<IActionResult> HistoryExport([FromQuery] string? format, [FromQuery] string[]? columns, CancellationToken ct)
    {
        if (!HasAny(PolicyRead, PolicyManage)) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        if (!HistoryColumns().TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);
        // WP-VW-W3L — the screen's scope filter, and the scope column by NAME (the lists the page reads)
        if (HistoryScopeQuery(Request.Query["scope"], Request.Query["scopeRef"]) is not { } scopeQuery) return ExportRefusal(400, "scope must be tenant | legal-entity | business-unit (with its scopeRef).", "visit_evidence_scope_invalid");

        var rows = new List<JsonElement>();
        var people = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; ; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, $"{Policy}/history?page={page}&pageSize=200{scopeQuery}", null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            if (await ListPageOf(response, ct) is not { } chunk) return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
            if (chunk.Total > ListExportContract.MaxRows)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge, $"{chunk.Total} changes match; an export carries at most {ListExportContract.MaxRows}.", ListExportContract.TooLargeCode);
            }

            if (chunk.Names is { } names) foreach (var p in names.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.String) people[p.Name] = p.Value.GetString()!;
            rows.AddRange(chunk.Items.EnumerateArray());
            if (chunk.Items.GetArrayLength() < 200 || rows.Count >= chunk.Total) break;
        }

        var lines = rows.Select(r => new HistoryExportRow(
            r.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.String && a.TryGetDateTimeOffset(out var at) ? at : null,
            Str(r, "by") is { } by ? people.GetValueOrDefault(by) : null,
            Str(r, "field"), Str(r, "oldValue"), Str(r, "newValue"), Str(r, "kind") ?? "set",
            Str(r, "scope") ?? "tenant", Str(r, "scopeRef"))).ToList();
        // the scope names only once the file will be written (a refused export reads nothing more)
        _exportScopeNames = exportColumns.Any(c => c.Key == "scope") ? await ScopeNamesAsync(ct) : new Dictionary<string, string>();
        var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text("PaneHistory", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("visit-evidence-policy-history", DateTimeOffset.UtcNow, fileFormat));
    }

    /// <summary>One change as the export writes it (the person by name — never the id); its scope (WP-VW-W3L).</summary>
    public sealed record HistoryExportRow(DateTimeOffset? At, string? By, string? Field, string? OldValue, string? NewValue, string Kind,
        string Scope = "tenant", string? ScopeRef = null);

    private ListExportColumnSet<HistoryExportRow> HistoryColumns() => new(
    [
        new("at", c => Text("ColDate", c), (r, c) => r.At?.ToString("g", c)),
        new("scope", c => Text("ColScope", c), (r, c) => ScopeText(r.Scope, r.ScopeRef, c)),
        new("by", c => Text("ColPerson", c), (r, _) => r.By ?? "—"),
        new("field", c => Text("ColField", c), (r, c) => r.Field is { Length: > 0 } f ? Text("F_" + f, c) : "—"),
        new("newValue", c => Text("ColChange", c), (r, c) => r.Kind switch
        {
            "created" => "— → " + Text(r.Scope == "tenant" ? "HistCreated" : "HistScopeCreated", c),
            "archived" => "— → " + Text("HistArchived", c),
            _ => ValueText(r.Field, r.OldValue, c, r.Scope != "tenant") + " → " + ValueText(r.Field, r.NewValue, c, r.Scope != "tenant")
        })
    ]);

    /// <summary>The scope names of the export (filled per request from the lists the page reads).</summary>
    private IReadOnlyDictionary<string, string> _exportScopeNames = new Dictionary<string, string>();

    /// <summary>A row's scope by NAME — the tenant's label, a layer's MDM / MOD-0048 name, its code only when no list knows it.</summary>
    private string ScopeText(string scope, string? scopeRef, CultureInfo culture)
    {
        if (scope is not ("legal-entity" or "business-unit")) return Text("TenantDefault", culture);
        var key = scope + ":" + (scope == "legal-entity" ? scopeRef?.ToLowerInvariant() : scopeRef);
        return _exportScopeNames.TryGetValue(key, out var name) ? name : scopeRef ?? "—";
    }

    /// <summary>"legal-entity:&lt;id&gt;" / "business-unit:&lt;code&gt;" → name, from the two option lists (an unreadable list adds none).</summary>
    private async Task<Dictionary<string, string>> ScopeNamesAsync(CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var le = await SendGatewayAsync(HttpMethod.Get, "/api/legal-entities/lookup", null, ct))
        {
            if (le is { IsSuccessStatusCode: true } && LegalEntityOptionsOf(await le.Content.ReadAsStringAsync(ct)) is { } entities)
            {
                foreach (var o in entities) names["legal-entity:" + o.Ref.ToLowerInvariant()] = o.Name;
            }
        }

        var tenantId = GetTenantId();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            using var bu = await _referenceSets.ReadAsync(BusinessUnitSetCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), tenantId, ct);
            if (bu is { IsSuccessStatusCode: true } && BusinessUnitOptionsOf(await bu.Content.ReadAsStringAsync(ct)) is { } units)
            {
                foreach (var o in units) names["business-unit:" + o.Ref] = o.Name;
            }
        }

        return names;
    }

    /// <summary>A history value in the reader's words — the page's valueKey rule (evidence-core.js): a switch On / Off, a
    /// number with its unit, a choice its option text; a layer's empty side is "from above" (WP-VW-W3L).</summary>
    private string ValueText(string? field, string? raw, CultureInfo culture, bool layer = false)
    {
        if (layer && !string.IsNullOrEmpty(field) && raw is null) return Text("HistInherited", culture);
        if (string.IsNullOrEmpty(field) || raw is null) return "—";
        if (Switches.Contains(field)) return Text(raw is "true" or "True" ? "ValueOn" : "ValueOff", culture);
        if (NumberUnits.TryGetValue(field, out var unit)) return Text(unit, culture).Replace("{0}", raw, StringComparison.Ordinal);
        return Text("Opt_" + field + "_" + raw, culture);
    }

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<VisitEvidenceSettingsIndex>)) as IStringLocalizer<VisitEvidenceSettingsIndex>;
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

    private IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });

    private BadRequestObjectResult Refused(string message) => BadRequest(new { errors = new[] { message, "visit_evidence_policy_invalid" } });

    private static string? Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [HttpGet("api/policy/effective")]
    public Task<IActionResult> Effective(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/effective{Request.QueryString}", null, ct, PolicyRead, PolicyManage);

    [HttpGet("api/policy/reps")]
    public Task<IActionResult> Reps(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/reps{Request.QueryString}", null, ct, PolicyRead, PolicyManage);

    // ---------------- WP-VW-W3L (W3-WEB-e) — the scope layers ----------------

    /// <summary>The layers a manager adds under the tenant (the tenant layer is the policy itself).</summary>
    public static readonly IReadOnlyList<string> LayerTypes = ["legal-entity", "business-unit"];

    /// <summary>
    /// Static, so MVC never treats it as an action: the CRM path of one layer, or null when the key is not one (a legal entity
    /// is a Guid — normalized "D" —, a business unit a code of at most 100 characters). Refused before any CRM call.
    /// </summary>
    public static string? ScopePath(string? scopeType, string? scopeRef)
    {
        var type = scopeType?.Trim().ToLowerInvariant();
        var key = scopeRef?.Trim();
        if (type is null || !LayerTypes.Contains(type) || string.IsNullOrEmpty(key)) return null;
        if (type == "legal-entity")
        {
            return Guid.TryParse(key, out var id) && id != Guid.Empty ? $"{Policy}/scopes/{type}/{id:D}" : null;
        }

        return key.Length <= 100 ? $"{Policy}/scopes/{type}/{Uri.EscapeDataString(key)}" : null;
    }

    /// <summary>Static, so MVC never treats it as an action: the history read's scope filter (<c>scope</c> tenant |
    /// legal-entity | business-unit, <c>scopeRef</c> for a layer) → the CRM query suffix ("" = every scope); null = refused.</summary>
    public static string? HistoryScopeQuery(string? scope, string? scopeRef)
    {
        var type = scope?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(type)) return string.Empty;
        if (type == "tenant") return "&scope=tenant";
        return ScopePath(type, scopeRef) is null ? null : $"&scope={type}&scopeRef={Uri.EscapeDataString(scopeRef!.Trim())}";
    }

    [HttpGet("api/policy/scopes")]
    public Task<IActionResult> ListScopes(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Policy}/scopes", null, ct, PolicyRead, PolicyManage);

    [HttpGet("api/policy/scopes/{scopeType}/{scopeRef}")]
    public Task<IActionResult> GetScope(string scopeType, string scopeRef, CancellationToken ct)
        => ScopePath(scopeType, scopeRef) is { } path
            ? ProxyAsync(HttpMethod.Get, path, null, ct, PolicyRead, PolicyManage)
            : ScopeRefusal(PolicyRead, PolicyManage);

    /// <summary>Create a layer, or replace its overrides at <c>expectedVersion</c> (the body carries every field; null =
    /// from above). The CRM refuses an empty layer (400) and a stale version (409).</summary>
    [HttpPut("api/policy/scopes/{scopeType}/{scopeRef}")]
    public Task<IActionResult> PutScope(string scopeType, string scopeRef, CancellationToken ct)
        => ScopePath(scopeType, scopeRef) is { } path
            ? ProxyBodyAsync(HttpMethod.Put, path, ct, PolicyManage)
            : ScopeRefusal(PolicyManage);

    /// <summary>"Remove the scope": archived at <c>expectedVersion</c>, its reps fall back to the layer below at once.</summary>
    [HttpDelete("api/policy/scopes/{scopeType}/{scopeRef}")]
    public Task<IActionResult> RemoveScope(string scopeType, string scopeRef, [FromQuery] int? expectedVersion, CancellationToken ct)
        => ScopePath(scopeType, scopeRef) is { } path
            ? ProxyAsync(HttpMethod.Delete, path + (expectedVersion is { } v ? "?expectedVersion=" + v.ToString(CultureInfo.InvariantCulture) : string.Empty), null, ct, PolicyManage)
            : ScopeRefusal(PolicyManage);

    /// <summary>A key that is not a layer: 400 for a caller who may make the call, 403 otherwise (no CRM call either way).</summary>
    private Task<IActionResult> ScopeRefusal(params string[] anyOf)
        => Task.FromResult<IActionResult>(HasAny(anyOf)
            ? BadRequest(new { errors = new[] { "scopeType legal-entity | business-unit; a legal entity scopeRef is a Guid.", "visit_evidence_scope_invalid" } })
            : Denied());

    /// <summary>
    /// "Add a legal entity" and the scope names: the tenant's referenceable MDM legal entities → <c>{ available, items: [{ ref,
    /// name }] }</c> (the name, never the code beside it). MDM refusing or down → <c>available = false</c> and no items: the
    /// page names a layer by its id then, never by a guess.
    /// </summary>
    [HttpGet("api/scope-options/legal-entities")]
    public async Task<IActionResult> LegalEntityOptions(CancellationToken ct)
    {
        if (!HasAny(PolicyRead, PolicyManage)) return Denied();
        using var response = await SendGatewayAsync(HttpMethod.Get, "/api/legal-entities/lookup", null, ct);
        var items = response is { IsSuccessStatusCode: true } ? LegalEntityOptionsOf(await response.Content.ReadAsStringAsync(ct)) : null;
        return Ok(new { data = new { available = items is not null, items = (IEnumerable<ScopeOption>?)items ?? [] } });
    }

    /// <summary>"Add a business unit" and the scope names: the tenant's PUBLISHED MOD-0048 business-unit values (the set the
    /// territory model reads; the CRM validates a layer's code against it) → <c>{ available, items: [{ ref: code, name: label }] }</c>.</summary>
    [HttpGet("api/scope-options/business-units")]
    public async Task<IActionResult> BusinessUnitOptions(CancellationToken ct)
    {
        if (!HasAny(PolicyRead, PolicyManage)) return Denied();
        var tenantId = GetTenantId();
        using var response = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : await _referenceSets.ReadAsync(BusinessUnitSetCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), tenantId, ct);
        var items = response is { IsSuccessStatusCode: true } ? BusinessUnitOptionsOf(await response.Content.ReadAsStringAsync(ct)) : null;
        return Ok(new { data = new { available = items is not null, items = (IEnumerable<ScopeOption>?)items ?? [] } });
    }

    /// <summary>One scope a manager may pick: its ref (legal entity id "D" / business unit code) and its name.</summary>
    public sealed record ScopeOption(string Ref, string Name);

    /// <summary>MDM <c>GET /api/legal-entities/lookup</c> → the referenceable entities by name (display name, else legal
    /// name, else code); null when the body is not the lookup.</summary>
    public static List<ScopeOption>? LegalEntityOptionsOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Array) return null;
            var options = new List<ScopeOption>();
            foreach (var item in data.EnumerateArray())
            {
                if (!Guid.TryParse(Str(item, "legalEntityId") ?? Str(item, "id"), out var id) || id == Guid.Empty) continue;
                if (item.TryGetProperty("referenceable", out var r) && r.ValueKind == JsonValueKind.False) continue;
                var name = new[] { Str(item, "displayName"), Str(item, "legalName"), Str(item, "code") }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                options.Add(new ScopeOption(id.ToString("D"), name?.Trim() ?? id.ToString("D")));
            }

            return options;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The published values model (<c>{ data: { items: [{ code, label, isActive }] } }</c>) → the active values by
    /// label; null when the body is not that model.</summary>
    public static List<ScopeOption>? BusinessUnitOptionsOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            var items = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var i) ? i : data;
            if (items.ValueKind != JsonValueKind.Array) return null;
            var options = new List<ScopeOption>();
            foreach (var item in items.EnumerateArray())
            {
                var code = Str(item, "code") ?? Str(item, "valueCode");
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (item.TryGetProperty("isActive", out var a) && a.ValueKind == JsonValueKind.False) continue;
                var label = Str(item, "label");
                options.Add(new ScopeOption(code.Trim(), string.IsNullOrWhiteSpace(label) ? code.Trim() : label.Trim()));
            }

            return options;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---------------- the time-boxed exceptions ----------------

    [HttpGet("api/exceptions")]
    public Task<IActionResult> ListExceptions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Exceptions}{Request.QueryString}", null, ct, ExceptionRead);

    [HttpGet("api/exceptions/{id:guid}")]
    public Task<IActionResult> GetException(Guid id, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Exceptions}/{id}", null, ct, ExceptionRead);

    [HttpPost("api/exceptions")]
    public Task<IActionResult> CreateException(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, Exceptions, ct, ExceptionManage);

    [HttpPut("api/exceptions/{id:guid}")]
    public Task<IActionResult> UpdateException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Put, $"{Exceptions}/{id}", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/submit")]
    public Task<IActionResult> SubmitException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/submit", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/withdraw")]
    public Task<IActionResult> WithdrawException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/withdraw", ct, ExceptionManage);

    /// <summary>The reviewer's decision: the read key (the Work Center task decides who may — the CRM rule).</summary>
    [HttpPost("api/exceptions/{id:guid}/decision")]
    public Task<IActionResult> DecideException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/decision", ct, ExceptionRead);

    [HttpPost("api/exceptions/{id:guid}/cancel")]
    public Task<IActionResult> CancelException(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/cancel", ct, ExceptionManage);

    [HttpPost("api/exceptions/{id:guid}/end-early")]
    public Task<IActionResult> EndExceptionEarly(Guid id, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"{Exceptions}/{id}/end-early", ct, ExceptionManage);

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyBodyAsync(HttpMethod method, string path, CancellationToken ct, params string[] anyOf)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(method, path, body ?? "{}", ct, anyOf);
    }

    /// <summary>The caller needs ANY of <paramref name="anyOf"/> (one key for a write; read OR manage for a policy read).</summary>
    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] anyOf)
    {
        if (!HasAny(anyOf))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, rawBody, ct);
        return await ToProxyResultAsync(response, ct);
    }

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit evidence settings Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        // A bodiless upstream status (204/304/…) must not be turned into a body — the same-origin-proxy 204→500 trap.
        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        if (CrmVisitExecutionController.ProblemDetailsToEnvelope(
                response.Content.Headers.ContentType?.MediaType, content, (int)response.StatusCode) is { } envelope)
        {
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = envelope };
        }

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent
            or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private static bool ContainsTenantId(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAny(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));
}
