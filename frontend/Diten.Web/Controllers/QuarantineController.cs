using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0175 Q5 — Karantina / Bloke Stok tenant pages + their same-origin doors (pack §9, §11.1–§11.3, §14). Tenant shell only:
// every route is /Quarantine/**, never /Platform (ScopeFromRoute = Tenant). The controller holds no quarantine rule — every
// decision, signature check and stock rule is InventoryService's (QUARANTINE-BUNDLE 1.0.0, frozen):
//   • api/{**everything}  → Gateway /api/quarantine/{everything}  (the E5W1 ESignatureController / WorkflowController proxy
//     pattern: the caller's own token from the auth cookie, TenantId from the token — never from the browser — correlation id,
//     Accept-Language, Idempotency-Key and If-Match passed through, ETag passed back). Writes carry the antiforgery token
//     (the MOD-0174 Lots pattern). Bodies are forwarded as they are and NEVER logged; the log line holds method, path, status.
//   • list/{holds|blocked|retest} → the server-mode list door (frontend-datatable-template.md "Sunucu modu sözleşmesi"):
//     start / length / search → page / pageSize / q; the service order is the contract's (open first, most overdue).
//   • data/{hold|blocked|decision}/{id} → one call per page: the record + its ETag + display names (MasterLabelResolver,
//     the existing label resolver of the inventory screens) + the company settings the page needs.
//   • lookups/{kind} → pickers of the forms (warehouses, locations, lots, AVAILABLE stock rows, suppliers, positions).
// The W-0173-02 company rule is the one of TraceGatewayClient: a token without legal_entity_id sends the selected company as
// X-Legal-Entity-Id; with no company selected nothing is called (COMPANY_NOT_SELECTED). The page check is UAS-001 display only
// (in the views); InventoryService's [HasPermission] stays the authority.
[Authorize]
[Route("Quarantine")]
public sealed class QuarantineController : Controller
{
    public const string ApiBase = "/api/quarantine";
    private const string ViewRoot = "~/Views/Quarantine";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const int MaxServicePageSize = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<Diten.Web.Views.Quarantine.Quarantine> _localizer;
    private readonly ILogger<QuarantineController> _logger;

    public QuarantineController(
        IHttpClientFactory httpClientFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<Diten.Web.Views.Quarantine.Quarantine> localizer,
        ILogger<QuarantineController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // ── pages (pack §11.1; UAS-001 is the view's: _AccessDenied and nothing else without the page key, no redirect) ──────

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Details/{holdId}")]
    public IActionResult Details(string holdId) => View($"{ViewRoot}/Details.cshtml", PageId(holdId));

    [HttpGet("Decide/{holdId}")]
    public IActionResult Decide(string holdId) => View($"{ViewRoot}/Decide.cshtml", PageId(holdId));

    [HttpGet("Blocked")]
    public IActionResult Blocked() => View($"{ViewRoot}/Blocked.cshtml");

    [HttpGet("Blocked/Disposition/{blockedId}")]
    public IActionResult Disposition(string blockedId) => View($"{ViewRoot}/Disposition.cshtml", PageId(blockedId));

    [HttpGet("Retest")]
    public IActionResult Retest() => View($"{ViewRoot}/Retest.cshtml");

    [HttpGet("Decisions/{decisionId}/Submit")]
    public IActionResult Submit(string decisionId) => View($"{ViewRoot}/Submit.cshtml", PageId(decisionId));

    [HttpGet("Settings")]
    public IActionResult Settings() => View($"{ViewRoot}/Settings.cshtml");

    // ── QUARANTINE-BUNDLE proxy ───────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api/{**everything}")]
    public Task<IActionResult> ProxyRead(string? everything) => ProxyAsync(everything);

    [HttpPost("api/{**everything}")]
    [HttpPut("api/{**everything}")]
    [HttpPatch("api/{**everything}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ProxyWrite(string? everything) => ProxyAsync(everything);

    // ── server-mode lists (one door per list; the service's own order) ─────────────────────────────────────────────

    [HttpGet("list/holds")]
    public async Task<IActionResult> ListHolds(CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var query = new QueryBuilder()
            .Add("page", page).Add("pageSize", size).Add("q", Search())
            .Add("warehouseId", Request.Query["warehouseId"]).Add("stockStatus", Request.Query["stockStatus"])
            .Add("source", Request.Query["source"]).Add("lateOnly", Request.Query["lateOnly"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/holds{query}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("listHolds", reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var items = root["items"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(items, ct);
        Decorate(items, labels);
        var total = (int?)root["total"] ?? items.Count;
        return ListJson(items, total, labels.Available, new JsonObject { ["kpis"] = root["kpis"]?.DeepClone() });
    }

    [HttpGet("list/blocked")]
    public async Task<IActionResult> ListBlocked(CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var query = new QueryBuilder()
            .Add("page", page).Add("pageSize", size).Add("q", Search())
            .Add("blockReasonCode", Request.Query["blockReasonCode"]).Add("pendingOnly", Request.Query["pendingOnly"])
            .Add("warehouseId", Request.Query["warehouseId"]).Add("status", Request.Query["status"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/blocked{query}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("listBlocked", reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var items = root["items"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(items, ct);
        Decorate(items, labels);
        return ListJson(items, (int?)root["total"] ?? items.Count, labels.Available, null);
    }

    /// <summary>listRetest answers the whole horizon (an array, no paging); the door pages and searches it here.</summary>
    [HttpGet("list/retest")]
    public async Task<IActionResult> ListRetest(CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        var query = new QueryBuilder().Add("horizonDays", Request.Query["horizonDays"]).Add("state", Request.Query["state"])
            .Add("warehouseId", Request.Query["warehouseId"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/retest{query}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("listRetest", reply);
        var all = JsonNode.Parse(reply.Body) as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(all, ct);
        Decorate(all, labels);
        var term = Search();
        var filtered = all.Where(n => n is JsonObject o && (term is null
                || Contains(o["lotNumber"], term) || Contains(o["item"]?["name"], term) || Contains(o["itemLabel"], term) || Contains(o["skuLabel"], term)))
            .ToList();
        var (start, length) = Window();
        var pageItems = new JsonArray(filtered.Skip(start).Take(length).Select(n => n!.DeepClone()).ToArray());
        return ListJson(pageItems, filtered.Count, labels.Available, new JsonObject { ["total"] = all.Count });
    }

    // ── one call per page ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Kayıt Detayı / Karar Ver: the hold (+ ETag), its active decision, names, and the company settings.</summary>
    [HttpGet("data/hold/{holdId}")]
    public async Task<IActionResult> HoldData(string holdId, CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        if (!Guid.TryParse(holdId, out var id)) return Refuse(404, "QUARANTINE_HOLD_NOT_FOUND");
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/holds/{id:D}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("getHold", reply);
        var hold = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var labels = await LabelsAsync(new JsonArray(hold.DeepClone()), ct);
        Decorate(hold, labels);
        var decision = await ActiveDecisionAsync(hold, ct);
        var settings = await SettingsAsync(ct);
        return Json(new JsonObject
        {
            ["hold"] = hold,
            ["etag"] = reply.ETag,
            ["decision"] = decision,
            ["settings"] = settings,
            ["labelsAvailable"] = labels.Available,
            ["me"] = CurrentUserId()
        });
    }

    /// <summary>Disposition / Blokeyi Kaldır: the blocked record (+ ETag), its active decision, names, settings, block reasons.</summary>
    [HttpGet("data/blocked/{blockedId}")]
    public async Task<IActionResult> BlockedData(string blockedId, CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        if (!Guid.TryParse(blockedId, out var id)) return Refuse(404, "QUARANTINE_BLOCKED_NOT_FOUND");
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/blocked/{id:D}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("getBlocked", reply);
        var blocked = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var settings = await SettingsAsync(ct);
        var sampleLocation = settings?["sampleLocations"]?[(string?)blocked["warehouse"]?["id"] ?? string.Empty]?.GetValue<string>();
        var labels = await LabelsAsync(new JsonArray(blocked.DeepClone()), ct, sampleLocation);
        Decorate(blocked, labels);
        return Json(new JsonObject
        {
            ["blocked"] = blocked,
            ["etag"] = reply.ETag,
            ["decision"] = await ActiveDecisionAsync(blocked, ct),
            ["settings"] = settings,
            ["sampleLocation"] = sampleLocation is null ? null : new JsonObject { ["id"] = sampleLocation, ["label"] = labels.Location(sampleLocation) ?? sampleLocation },
            ["labelsAvailable"] = labels.Available,
            ["me"] = CurrentUserId()
        });
    }

    /// <summary>Karar Gönder: the decision, its record (hold / blocked) or lot, names.</summary>
    [HttpGet("data/decision/{decisionId}")]
    public async Task<IActionResult> DecisionData(string decisionId, CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        if (!Guid.TryParse(decisionId, out var id)) return Refuse(404, "QUARANTINE_DECISION_NOT_FOUND");
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/decisions/{id:D}", null, null, null, ct);
        if (!reply.IsSuccess) return Failed("getDecision", reply);
        var decision = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        JsonObject? record = null;
        if ((string?)decision["holdId"] is { } holdId)
        {
            var hold = await SendAsync(HttpMethod.Get, $"{ApiBase}/holds/{Uri.EscapeDataString(holdId)}", null, null, null, ct);
            record = hold.IsSuccess ? JsonNode.Parse(hold.Body) as JsonObject : null;
        }
        else if ((string?)decision["blockedStockId"] is { } blockedId)
        {
            var blocked = await SendAsync(HttpMethod.Get, $"{ApiBase}/blocked/{Uri.EscapeDataString(blockedId)}", null, null, null, ct);
            record = blocked.IsSuccess ? JsonNode.Parse(blocked.Body) as JsonObject : null;
        }

        JsonObject? lot = null;
        if (record is null && (string?)decision["lotId"] is { } lotId && Guid.TryParse(lotId, out var lotGuid))
        {
            var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
            var lotReply = trace is null ? null : await trace.GetAsync($"/api/trace/lots/{lotGuid:D}", ct);
            lot = lotReply is { IsSuccess: true } ? JsonNode.Parse(lotReply.Body) as JsonObject : null;
        }

        var labels = await LabelsAsync(record is null ? new JsonArray() : new JsonArray(record.DeepClone()), ct);
        if (record is not null) Decorate(record, labels);
        return Json(new JsonObject { ["decision"] = decision, ["record"] = record, ["lot"] = lot, ["labelsAvailable"] = labels.Available, ["me"] = CurrentUserId() });
    }

    // ── pickers ──────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? q, [FromQuery] string? warehouseId, [FromQuery] string? lotId, CancellationToken ct)
    {
        if (!_permissions.Has(QuarantineKeys.HoldsRead)) return Refuse(403, null);
        switch (kind)
        {
            case "warehouses":
            case "locations":
            {
                var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
                if (ledger is null) return Refuse(401, null);
                var path = kind == "warehouses"
                    ? "/api/locations?type=Warehouse"
                    : "/api/locations/search?isStockable=true&status=Active&page=1&pageSize=200";
                var reply = await ledger.GetAsync(path, ct);
                return reply.IsSuccess ? Json(new { items = Options(reply.Body, "id", "code", "name") }) : Failed("lookups." + kind, reply);
            }
            case "lots":
            {
                var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
                if (trace is null) return Refuse(401, null);
                var reply = await trace.GetAsync($"/api/trace/lots?q={Uri.EscapeDataString(q ?? string.Empty)}&page=1&pageSize=20", ct);
                return reply.IsSuccess ? Json(new { items = Options(reply.Body, "id", "lotNumber", null) }) : Failed("lookups.lots", reply);
            }
            case "stock":
            {
                // AVAILABLE rows of one lot — the buckets "Stoğu bloke et" may name (blockStock, safety direction).
                if (!Guid.TryParse(lotId, out var lot)) return Refuse(400, "INVALID_REQUEST");
                var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
                if (ledger is null) return Refuse(401, null);
                var reply = await ledger.GetAsync($"/api/inventory/balance?lotId={lot:D}&stockStatus=AVAILABLE&page=1&pageSize=200", ct);
                if (!reply.IsSuccess) return Failed("lookups.stock", reply);
                var rows = (JsonNode.Parse(reply.Body)?["rows"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                    .Where(r => decimal.TryParse((string?)r["onHand"] ?? r["onHand"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var onHand) && onHand > 0m)
                    .ToList();
                var labels = await MasterLabelResolver.ResolveAsync(ledger, rows.Select(r => (string?)r["itemId"]),
                    rows.Select(r => ((string?)r["skuId"], (string?)r["skuLevel"])), rows.SelectMany(r => new[] { (string?)r["warehouseId"], (string?)r["locationId"] }), ct);
                return Json(new
                {
                    items = rows.Select(r => new
                    {
                        itemId = (string?)r["itemId"], skuId = (string?)r["skuId"], skuLevel = (string?)r["skuLevel"],
                        warehouseId = (string?)r["warehouseId"], locationId = (string?)r["locationId"], lotId = lot.ToString("D"),
                        onHand = r["onHand"]?.ToString(), baseUomId = (string?)r["baseUomId"],
                        item = labels.Product((string?)r["itemId"]), warehouse = labels.Location((string?)r["warehouseId"]),
                        location = labels.Location((string?)r["locationId"])
                    }),
                    labelsAvailable = labels.Available
                });
            }
            case "suppliers":
            {
                var reply = await SendAsync(HttpMethod.Get, "/api/suppliers", null, null, null, ct, company: false);
                return reply.IsSuccess ? Json(new { items = Options(reply.Body, "supplierId", "name", null) }) : Failed("lookups.suppliers", reply);
            }
            case "positions":
            {
                var reply = await SendAsync(HttpMethod.Get, "/api/v1/workflow/lookups/positions", null, null, null, ct, company: false);
                return reply.IsSuccess ? Json(new { items = Options(reply.Body, "id", "code", "name") }) : Failed("lookups.positions", reply);
            }
            default:
                return Refuse(404, null);
        }
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> ProxyAsync(string? everything)
    {
        var path = string.IsNullOrWhiteSpace(everything) ? ApiBase : $"{ApiBase}/{everything.TrimStart('/')}";
        string? body = null;
        if (!HttpMethods.IsGet(Request.Method) && Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var reply = await SendAsync(new HttpMethod(Request.Method), path + Request.QueryString, body,
            Header("Idempotency-Key"), Header("If-Match"), HttpContext.RequestAborted);
        if (reply.ETag is not null) Response.Headers.ETag = reply.ETag;
        return new ContentResult { StatusCode = reply.StatusCode == 0 ? 503 : reply.StatusCode, Content = reply.Body, ContentType = "application/json" };
    }

    private async Task<Reply> SendAsync(HttpMethod method, string pathAndQuery, string? body, string? idempotencyKey, string? ifMatch,
        CancellationToken ct, bool company = true)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Reply.Error(401, "UNAUTHORIZED");
        var tenantId = ResolveTenantId(token);
        if (tenantId is null) return Reply.Error(401, "UNAUTHORIZED");

        Guid? selected = null;
        if (company && CompanySelection.TokenLegalEntity(User) is null)
        {
            selected = CompanySelection.SelectedCompany(Request, User);
            if (selected is null) return Reply.Error(403, InventoryGatewayClient.CompanyNotSelected);
        }

        var correlationId = Request.Headers.TryGetValue(CorrelationHeaderName, out var c) && !string.IsNullOrWhiteSpace(c.ToString()) ? c.ToString() : HttpContext.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            request.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId);
            if (selected is not null) request.Headers.TryAddWithoutValidation(CompanySelection.LegalEntityHeader, selected.Value.ToString("D"));
            if (Header("Accept-Language") is { } language) request.Headers.TryAddWithoutValidation("Accept-Language", language);
            if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            _logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "Quarantine proxy completed. method={Method} path={Path} status={StatusCode} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, pathAndQuery.Split('?')[0], (int)response.StatusCode, correlationId, stopwatch.ElapsedMilliseconds);
            return new Reply((int)response.StatusCode, text, response.Headers.ETag?.ToString());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Quarantine proxy unreachable. method={Method} path={Path} correlation_id={CorrelationId}", method.Method, pathAndQuery.Split('?')[0], correlationId);
            return Reply.Error(503, "SERVICE_UNAVAILABLE");
        }
    }

    private async Task<JsonObject?> ActiveDecisionAsync(JsonObject record, CancellationToken ct)
    {
        if ((string?)record["activeDecision"]?["decisionId"] is not { } decisionId) return null;
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/decisions/{Uri.EscapeDataString(decisionId)}", null, null, null, ct);
        return reply.IsSuccess ? JsonNode.Parse(reply.Body) as JsonObject : null;
    }

    private async Task<JsonObject?> SettingsAsync(CancellationToken ct)
    {
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/settings", null, null, null, ct);
        return reply.IsSuccess ? JsonNode.Parse(reply.Body) as JsonObject : null;
    }

    /// <summary>Display names of the ids on this page (products, SKUs, warehouses, locations) — MasterLabelResolver.</summary>
    private async Task<MasterLabels> LabelsAsync(JsonArray records, CancellationToken ct, string? extraLocation = null)
    {
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return MasterLabels.None;
        var rows = records.OfType<JsonObject>().ToList();
        var locations = rows.SelectMany(r => new[] { (string?)r["warehouse"]?["id"], (string?)r["location"]?["id"] })
            .Concat(rows.SelectMany(r => (r["buckets"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().SelectMany(b => new[] { (string?)b["warehouseId"], (string?)b["locationId"] })))
            .Append(extraLocation);
        return await MasterLabelResolver.ResolveAsync(ledger, rows.Select(r => (string?)r["item"]?["itemId"]),
            rows.Select(r => ((string?)r["item"]?["skuId"], (string?)r["item"]?["skuLevel"])), locations, ct);
    }

    private static void Decorate(JsonArray records, MasterLabels labels)
    {
        foreach (var record in records.OfType<JsonObject>()) Decorate(record, labels);
    }

    /// <summary>Adds itemLabel / skuLabel / warehouseLabel / locationLabel next to the ids (a missing name stays the raw id).</summary>
    private static void Decorate(JsonObject record, MasterLabels labels)
    {
        var item = record["item"];
        record["itemLabel"] = labels.Product((string?)item?["itemId"]) ?? (string?)item?["name"] ?? (string?)item?["itemId"];
        record["skuLabel"] = labels.Sku((string?)item?["skuId"]) ?? (string?)item?["skuCode"];
        if (record["warehouse"] is JsonObject warehouse)
            record["warehouseLabel"] = labels.Location((string?)warehouse["id"]) ?? (string?)warehouse["code"] ?? (string?)warehouse["id"];
        if (record["location"] is JsonObject location)
            record["locationLabel"] = labels.Location((string?)location["id"]) ?? (string?)location["code"] ?? (string?)location["id"];
        if (record["buckets"] is JsonArray buckets)
        {
            foreach (var bucket in buckets.OfType<JsonObject>())
            {
                bucket["warehouseLabel"] = labels.Location((string?)bucket["warehouseId"]) ?? (string?)bucket["warehouseId"];
                bucket["locationLabel"] = labels.Location((string?)bucket["locationId"]) ?? (string?)bucket["locationId"];
            }
        }
    }

    private IActionResult ListJson(JsonArray items, int total, bool labelsAvailable, JsonObject? extra)
    {
        // the page's items may still belong to the service answer they were read from: a node has one parent
        var data = new JsonObject { ["items"] = items.Parent is null ? items : items.DeepClone(), ["total"] = total, ["filteredTotal"] = total };
        var body = new JsonObject { ["data"] = data, ["labelsAvailable"] = labelsAvailable };
        if (extra is not null)
        {
            foreach (var (key, value) in extra.ToList())
            {
                extra.Remove(key);
                body[key] = value;
            }
        }

        return Content(body.ToJsonString(), "application/json");
    }

    private IActionResult Json(JsonObject body) => Content(body.ToJsonString(), "application/json");

    private (int Page, int Size) Paging()
    {
        var (start, length) = Window();
        var size = Math.Clamp(length, 1, MaxServicePageSize);
        return (start / size + 1, size);
    }

    private (int Start, int Length) Window()
    {
        var start = int.TryParse(Request.Query["start"], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
        var length = int.TryParse(Request.Query["length"], NumberStyles.None, CultureInfo.InvariantCulture, out var l) && l > 0 ? l : 10;
        return (start, Math.Min(length, 500));
    }

    private string? Search()
    {
        var term = Request.Query["search"].ToString().Trim();
        return term.Length == 0 ? null : term;
    }

    private static bool Contains(JsonNode? value, string term)
        => value?.ToString().Contains(term, StringComparison.CurrentCultureIgnoreCase) == true;

    private static IEnumerable<object> Options(string body, string idName, string codeName, string? nameName)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(body); } catch (JsonException) { return []; }
        var list = root as JsonArray ?? root?["items"] as JsonArray ?? root?["data"] as JsonArray ?? root?["data"]?["items"] as JsonArray ?? new JsonArray();
        return list.OfType<JsonObject>().Select(o =>
        {
            var id = o[idName]?.ToString();
            var code = o[codeName]?.ToString();
            var name = nameName is null ? null : o[nameName]?.ToString();
            return (object)new { value = id, text = string.Join(" · ", new[] { code, name }.Where(x => !string.IsNullOrWhiteSpace(x))) };
        }).Where(o => o is not null);
    }

    private string? Header(string name) => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()) ? value.ToString() : null;

    private string? CurrentUserId()
        => User.Claims.FirstOrDefault(c => c.Type is "sub" or "userId" or "user_id" or ClaimTypes.NameIdentifier)?.Value;

    private static string? PageId(string? id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : null;

    private IActionResult Failed(string operation, Reply reply)
    {
        _logger.LogWarning("Quarantine {Operation} failed with {Status}.", operation, reply.StatusCode);
        var code = reply.Code;
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, code);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Quarantine {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Error?.Code);
    }

    /// <summary>A refusal in the reader's language (Quarantine.Errors.{CODE}); the service's own code is kept.</summary>
    private IActionResult Refuse(int status, string? code)
    {
        var key = code is null ? (status is 401 or 403 ? "Quarantine.Errors.FORBIDDEN" : status >= 500 ? "Quarantine.Errors.SERVICE_UNAVAILABLE" : "Quarantine.Errors.GENERIC")
            : "Quarantine.Errors." + code;
        var message = _localizer[key];
        return new JsonResult(new { error = new { code = code ?? "ERROR", message = message.ResourceNotFound ? _localizer["Quarantine.Errors.GENERIC"].Value : message.Value } })
        {
            StatusCode = status
        };
    }

    private string? ResolveTenantId(string token)
    {
        if (Guid.TryParse(FindTenantClaim(User.Claims), out var claimTenantId)) return claimTenantId.ToString();
        try
        {
            return Guid.TryParse(FindTenantClaim(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims), out var jwtTenantId) ? jwtTenantId.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindTenantClaim(IEnumerable<Claim> claims)
        => claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>A Gateway answer: status, body, ETag and the contract error code (INVENTORY Error schema).</summary>
    private sealed record Reply(int StatusCode, string Body, string? ETag)
    {
        public bool IsSuccess => StatusCode is >= 200 and < 300;

        public string? Code
        {
            get
            {
                try { return JsonNode.Parse(Body)?["error"]?["code"]?.GetValue<string>(); }
                catch (Exception) { return null; }
            }
        }

        public static Reply Error(int status, string code) => new(status, JsonSerializer.Serialize(new { error = new { code } }), null);
    }

    private sealed class QueryBuilder
    {
        private readonly List<string> _parts = [];

        public QueryBuilder Add(string name, object? value)
        {
            var text = value switch
            {
                null => null,
                Microsoft.Extensions.Primitives.StringValues values => values.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            if (!string.IsNullOrWhiteSpace(text)) _parts.Add($"{name}={Uri.EscapeDataString(text)}");
            return this;
        }

        public override string ToString() => _parts.Count == 0 ? string.Empty : "?" + string.Join('&', _parts);
    }
}

/// <summary>
/// MOD-0175 pack §14 — the keys this Web reads for DISPLAY (UAS-001 §4: absent, not disabled). InventoryService's
/// [HasPermission] stays the authority. Screen → button → action → key, verbatim from the pack table.
/// </summary>
public static class QuarantineKeys
{
    public const string HoldsRead = "quarantine.holds.read";
    public const string HoldsCreate = "quarantine.holds.create";
    public const string HoldsReview = "quarantine.holds.review";
    public const string DecisionsCreate = "quarantine.decisions.create";
    public const string DecisionsWithdraw = "quarantine.decisions.withdraw";
    public const string DecisionsApply = "quarantine.decisions.apply";
    public const string BlockedDispose = "quarantine.blocked.dispose";
    public const string BlockedUnblock = "quarantine.blocked.unblock";
    public const string BlockedBlock = "quarantine.blocked.block";
    public const string SettingsManage = "quarantine.settings.manage";

    /// <summary>Action code → key (pack §14 "Screen → button → action → permission").</summary>
    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DECIDE"] = DecisionsCreate,
        ["VIEWHOLD"] = HoldsRead,
        ["STARTREVIEW"] = HoldsReview,
        ["REFRESHWORKFLOW"] = HoldsRead,
        ["WITHDRAWDECISION"] = DecisionsWithdraw,
        ["REAPPLYDECISION"] = DecisionsApply,
        ["SUBMITDECISION"] = DecisionsCreate,
        ["DISPOSE"] = BlockedDispose,
        ["UNBLOCK"] = BlockedUnblock,
        ["COMPLETEREVIEW"] = HoldsReview,
        ["BLOCKSTOCK"] = BlockedBlock,
        ["SUBMITDISPOSITION"] = BlockedDispose,
        ["QUARANTINELOT"] = HoldsCreate,
        ["SAVESETTINGS"] = SettingsManage,
        ["PREPARETEMPLATE"] = SettingsManage
    };

    public static IReadOnlyDictionary<string, bool> For(IPermissionSnapshot permissions)
        => Actions.ToDictionary(a => a.Key, a => permissions.Has(a.Value), StringComparer.Ordinal);
}
