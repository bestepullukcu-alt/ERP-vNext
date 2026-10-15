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

// MOD-0177 R6 — Geri Çağırma tenant pages (pack §11.1, §11.3, §21): Hazırlık Paneli, İzleme, Vakalar. Tenant shell only: every route is
// /Recall/**, never /Platform. The controller holds no recall rule — every KPI, trace node and row is InventoryService's (RECALL-BUNDLE
// 1.5.0). There is NO catch-all proxy: each same-origin door names ONE gateway operation and asks for ITS key (the FIX-WEBGATES pattern —
// RecallDataGateTests finds the doors by reflection):
//   • data/dashboard        → getDashboard          (recall.dashboard.read)  + product names of the open-case rows
//   • data/trace            → runTrace              (recall.trace.run)       + product / warehouse names of the nodes
//   • export/trace          → exportTrace (CSV)     (recall.trace.export)    — the service's bytes unchanged (BOM, ';')
//   • data/cases (POST)     → openCase              (recall.cases.create)    — "Vaka aç" from İzleme: a real Draft case (D-22)
//   • list/cases            → listCases (union)     (recall.cases.read)      — server-mode DataTable door: cases + drill rows by position
//   • lookups/lots          → TRACE listLots        (recall.trace.run)       — the İzleme lot picker (TRACE's own key decides at the service)
//   • lookups/products      → listCases             (recall.cases.read)      — the products of the company's cases (Vakalar filter)
// The caller's own token, TenantId from the token, the W-0173-02 company rule (X-Legal-Entity-Id when the token names none), correlation
// id, Accept-Language, Idempotency-Key (writes) travel. Bodies are never logged. UAS-001 is the views' (display only).
[Authorize]
[Route("Recall")]
public sealed class RecallController : Controller
{
    public const string ApiBase = "/api/recall";
    private const string ViewRoot = "~/Views/Recall";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const int MaxServicePageSize = 200;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<Diten.Web.Views.Recall.Recall> _localizer;
    private readonly ILogger<RecallController> _logger;

    public RecallController(
        IHttpClientFactory httpClientFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<Diten.Web.Views.Recall.Recall> localizer,
        ILogger<RecallController> logger)
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

    [HttpGet("Trace")]
    public IActionResult Trace() => View($"{ViewRoot}/Trace.cshtml");

    [HttpGet("Cases")]
    public IActionResult Cases() => View($"{ViewRoot}/Cases.cshtml");

    // WP-0177-R7a — Vaka Detayı (RECALLCASEDETAILS, not in the menu; parent Vakalar). The id is the page's model; the view validates it.
    [HttpGet("Cases/Details/{id}")]
    public IActionResult CaseDetails(string id) => View($"{ViewRoot}/CaseDetails.cshtml", id);

    // ── Hazırlık Paneli ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>getDashboard (pack §8.9) — the KPIs as the service computed them + the product names of the open-case table.</summary>
    [HttpGet("data/dashboard")]
    public async Task<IActionResult> DashboardData(CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.DashboardRead)) return Refuse(403, null);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/dashboard", null, null, ct);
        if (!reply.IsSuccess) return Failed("getDashboard", reply);
        var panel = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var rows = panel["openCaseRows"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(rows, ct);
        Decorate(rows, labels);
        await PeopleAsync(ct, rows);
        return Json(new JsonObject { ["panel"] = panel.DeepClone(), ["labelsAvailable"] = labels.Available });
    }

    // ── İzleme ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>runTrace (pack §8.2) — nodes, edges, gaps as the service found them (CT-R8: another company only as a count) + names.</summary>
    [HttpGet("data/trace")]
    public async Task<IActionResult> TraceData([FromQuery] string? rootLotId, [FromQuery] string? direction, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.TraceRun)) return Refuse(403, null);
        if (TraceQuery(rootLotId, direction) is not { } query) return Refuse(400, "INVALID_REQUEST");
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/trace{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("runTrace", reply);
        var trace = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var nodes = trace["nodes"] as JsonArray ?? new JsonArray();
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        var list = nodes.OfType<JsonObject>().ToList();
        var labels = ledger is null
            ? MasterLabels.None
            : await MasterLabelResolver.ResolveAsync(ledger, list.Select(n => (string?)n["item"]?["itemId"]),
                list.Select(n => ((string?)n["item"]?["skuId"], (string?)n["item"]?["skuLevel"])),
                list.SelectMany(n => new[] { (string?)n["warehouseId"], (string?)n["locationId"] }), ct);
        foreach (var node in list)
        {
            node["itemLabel"] = labels.Product((string?)node["item"]?["itemId"]) ?? (string?)node["item"]?["itemId"];
            node["skuLabel"] = labels.Sku((string?)node["item"]?["skuId"]);
            node["warehouseLabel"] = labels.Location((string?)node["warehouseId"]) ?? (string?)node["warehouseId"];
            node["locationLabel"] = labels.Location((string?)node["locationId"]) ?? (string?)node["locationId"];
        }

        return Json(new JsonObject { ["trace"] = trace.DeepClone(), ["labelsAvailable"] = labels.Available });
    }

    /// <summary>exportTrace — the service's CSV bytes passed through unchanged (UTF-8 BOM, ';'); not an evidence export (pack §11.3).</summary>
    [HttpGet("export/trace")]
    public async Task<IActionResult> ExportTrace([FromQuery] string? rootLotId, [FromQuery] string? direction, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.TraceExport)) return Refuse(403, null);
        if (TraceQuery(rootLotId, direction) is not { } query) return Refuse(400, "INVALID_REQUEST");
        var file = await SendFileAsync($"{ApiBase}/trace/export{query}&format=csv", ct);
        if (file.Bytes is null) return Refuse(file.Status == 0 ? 503 : file.Status, file.Code);
        return File(file.Bytes, "text/csv; charset=utf-8", file.FileName ?? $"izleme_{rootLotId}.csv");
    }

    /// <summary>
    /// openCase — "Bu kapsamla geri çağırma vakası aç" (pack §11.3 RECALL_CASE_OPEN, D-22): a real Draft case whose scope the service fills from
    /// the trace. The body is forwarded as it is (rootLotId, direction, class, level, reason, reasonReference, idempotencyKey); the
    /// Idempotency-Key header is the body's key.
    /// </summary>
    [HttpPost("data/cases")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenCase(CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesCreate)) return Refuse(403, null);
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        string? key;
        try { key = (string?)JsonNode.Parse(body)?["idempotencyKey"]; }
        catch (JsonException) { return Refuse(400, "INVALID_REQUEST"); }
        if (string.IsNullOrWhiteSpace(key)) return Refuse(400, "INVALID_REQUEST");
        var reply = await SendAsync(HttpMethod.Post, $"{ApiBase}/cases", body, key, ct);
        if (!reply.IsSuccess) return Failed("openCase", reply);
        return new ContentResult { StatusCode = reply.StatusCode, Content = reply.Body, ContentType = "application/json" };
    }

    /// <summary>The İzleme lot picker — TRACE listLots of the company (the service checks TRACE's own key; this door needs recall.trace.run).</summary>
    [HttpGet("lookups/lots")]
    public async Task<IActionResult> LotLookup([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.TraceRun)) return Refuse(403, null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null);
        var reply = await trace.GetAsync($"/api/trace/lots?q={Uri.EscapeDataString(q ?? string.Empty)}&page=1&pageSize=20", ct);
        if (!reply.IsSuccess) return Failed("lookups.lots", reply);
        JsonNode? root;
        try { root = JsonNode.Parse(reply.Body); } catch (JsonException) { root = null; }
        var list = root as JsonArray ?? root?["items"] as JsonArray ?? root?["data"] as JsonArray ?? new JsonArray();
        return Json(new JsonObject
        {
            ["items"] = new JsonArray(list.OfType<JsonObject>()
                .Select(l => (JsonNode)new JsonObject { ["value"] = (string?)l["lotId"], ["text"] = (string?)l["lotNumber"] ?? (string?)l["lotId"] })
                .ToArray())
        });
    }

    // ── Vakalar ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// listCases (RECALL 1.5.0 union, CT decision A) as a server-mode DataTable door: DataTables paging / search → page / pageSize / q; the
    /// filters type, status (repeatable), drillStatus (repeatable), class, skuId, openedFrom are forwarded as they are. The answer's two arrays
    /// (cases in <c>items</c>, drill runs in <c>drillRows</c>) are laid out in ONE list by their <c>position</c>, each row tagged
    /// <c>rowKind</c> Case / Drill — a drill row keeps its own shape (no class, no level: D-25).
    /// </summary>
    [HttpGet("list/cases")]
    public async Task<IActionResult> ListCases(CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var query = new QueryBuilder().Add("page", page).Add("pageSize", size).Add("q", Search()).Add("type", Request.Query["type"])
            .Add("class", Request.Query["class"]).Add("skuId", Request.Query["skuId"]).Add("openedFrom", Request.Query["openedFrom"])
            .Add("sort", Request.Query["sort"]);
        foreach (var name in new[] { "status", "drillStatus" })
            foreach (var value in Request.Query[name].Where(v => !string.IsNullOrWhiteSpace(v)))
                query.Add(name, value);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/cases{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("listCases", reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var rows = Merge(root);
        var labels = await LabelsAsync(rows, ct);
        Decorate(rows, labels);
        await PeopleAsync(ct, rows);
        var total = (long?)root["total"] ?? rows.Count;
        return Json(new JsonObject
        {
            ["data"] = new JsonObject { ["items"] = rows, ["total"] = total, ["filteredTotal"] = total },
            ["statusCounts"] = root["statusCounts"]?.DeepClone(),
            ["drillStatusCounts"] = root["drillStatusCounts"]?.DeepClone(),
            ["labelsAvailable"] = labels.Available
        });
    }

    /// <summary>The products of the company's cases and drill runs (the Vakalar "Ürün" filter): skuId → name.</summary>
    [HttpGet("lookups/products")]
    public async Task<IActionResult> ProductLookup(CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesRead)) return Refuse(403, null);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/cases?page=1&pageSize={MaxServicePageSize}", null, null, ct);
        if (!reply.IsSuccess) return Failed("lookups.products", reply);
        var rows = Merge(JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject());
        var labels = await LabelsAsync(rows, ct);
        var items = rows.OfType<JsonObject>().Select(r => r["item"]).Where(i => !string.IsNullOrEmpty((string?)i?["skuId"]))
            .GroupBy(i => (string)i!["skuId"]!, StringComparer.Ordinal)
            .Select(g => (Sku: g.Key, Text: string.Join(" · ", new[] { labels.Product((string?)g.First()!["itemId"]) ?? (string?)g.First()!["itemId"], labels.Sku(g.Key) }
                .Where(x => !string.IsNullOrWhiteSpace(x)))))
            .OrderBy(x => x.Text, StringComparer.CurrentCulture)
            .Select(x => (JsonNode)new JsonObject { ["value"] = x.Sku, ["text"] = x.Text }).ToArray();
        return Json(new JsonObject { ["items"] = new JsonArray(items), ["labelsAvailable"] = labels.Available });
    }

    /// <summary>items (cases) + drillRows (drill runs) → one array in <c>position</c> order, each row tagged with its kind.</summary>
    // ── Vaka Detayı (WP-0177-R7a, pack §11.1 RECALLCASEDETAILS, §11.3) ─────────────────────────────────────────────

    /// <summary>
    /// The reads of the case detail page — ONE door, a closed list of parts (no free path): the case, its scope, the live diff, the stock,
    /// the customers, the returns, the effectiveness, the timeline. Product / warehouse / location names and people are added; nothing else.
    /// </summary>
    [HttpGet("data/cases/{id}")]
    public async Task<IActionResult> CaseData(string id, [FromQuery] string? part, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesRead)) return Refuse(403, null);
        if (!Guid.TryParse(id, out var caseId)) return Refuse(400, "INVALID_REQUEST");
        if (!CaseParts.TryGetValue(part ?? string.Empty, out var suffix)) return Refuse(400, "INVALID_REQUEST");
        var query = part == "timeline" ? new QueryBuilder().Add("page", Request.Query["page"]).Add("pageSize", Request.Query["pageSize"]).ToString() : string.Empty;
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/cases/{caseId:D}{suffix}{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("case " + (part ?? "detail"), reply);
        var data = JsonNode.Parse(reply.Body) ?? new JsonObject();
        var labels = await CaseLabelsAsync(data, ct);
        await PeopleAsync(ct, data);
        return Json(new JsonObject { ["data"] = data.DeepClone(), ["labelsAvailable"] = labels });
    }

    /// <summary>part → the service path after /cases/{id} (getCase, getCaseScope, getCaseScopeDiff, getCaseStock, getCaseCustomers, getCaseReturns, getCaseEffectiveness, getCaseTimeline).</summary>
    public static readonly IReadOnlyDictionary<string, string> CaseParts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [""] = "", ["scope"] = "/scope", ["diff"] = "/scope/diff", ["stock"] = "/stock", ["customers"] = "/customers", ["returns"] = "/returns",
        ["effectiveness"] = "/effectiveness", ["timeline"] = "/timeline"
    };

    /// <summary>markScopeItems — Dahil / Hariç (+ gerekçe ≥ 10), Draft.</summary>
    [HttpPost("cmd/cases/{id}/marks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkScope(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesUpdate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/scope/marks", "markScopeItems", null, ct);
    }

    /// <summary>submitScope — "Kapsam onayına gönder".</summary>
    [HttpPost("cmd/cases/{id}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitScope(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesUpdate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/scope/submit", "submitScope", null, ct);
    }

    /// <summary>returnScope — "Geri gönder" (gerekçe ≥ 10).</summary>
    [HttpPost("cmd/cases/{id}/return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnScope(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesApprove)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/scope/return", "returnScope", null, ct);
    }

    /// <summary>prepareCaseAction ApproveScope — step 1 of "Kapsamı onayla" (the door sets the action: this key prepares nothing else).</summary>
    [HttpPost("cmd/cases/{id}/prepare-approval")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareApproval(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesApprove)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/pending-action", "prepareCaseAction", "ApproveScope", ct);
    }

    /// <summary>approveScope — step 2 of "Kapsamı onayla" (signatureId from the MOD-0022 dialog when the policy asks for one).</summary>
    [HttpPost("cmd/cases/{id}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveScope(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesApprove)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/scope/approve", "approveScope", null, ct);
    }

    /// <summary>reviseScope — "Kapsam revizyonu" (CT-R5: the out-of-scope findings + gerekçe).</summary>
    [HttpPost("cmd/cases/{id}/revise")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviseScope(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesUpdate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/scope/revisions", "reviseScope", null, ct);
    }

    /// <summary>requestQuarantine — "Karantina isteği gönder" for lines without a request (KR-B).</summary>
    [HttpPost("cmd/cases/{id}/quarantine")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestQuarantine(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.QuarantineRequestsCreate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/quarantine-requests", "requestQuarantine", null, ct);
    }

    /// <summary>retryQuarantineRequest — "Yeniden dene".</summary>
    [HttpPost("cmd/cases/{id}/quarantine/{requestId}/retry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryQuarantine(string id, string requestId, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.QuarantineRequestsCreate)) return Refuse(403, null);
        if (!Guid.TryParse(requestId, out var request)) return Refuse(400, "INVALID_REQUEST");
        return await CaseCommandAsync(id, $"/quarantine-requests/{request:D}/retry", "retryQuarantineRequest", null, ct);
    }

    /// <summary>confirmSegregation — "Fiziksel ayırmayı teyit et" (record only, no movement).</summary>
    [HttpPost("cmd/cases/{id}/segregations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSegregation(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.SegregationsConfirm)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/segregations", "confirmSegregation", null, ct);
    }

    /// <summary>recordNotification — "Bilgilendirildi olarak kaydet" (KR-F, record only — no e-mail).</summary>
    [HttpPost("cmd/cases/{id}/notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordNotification(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.NotificationsCreate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/notifications", "recordNotification", null, ct);
    }

    /// <summary>declareConsumption — "Tüketim beyanı ekle".</summary>
    [HttpPost("cmd/cases/{id}/consumptions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclareConsumption(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.ConsumptionsCreate)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/consumption-declarations", "declareConsumption", null, ct);
    }

    /// <summary>linkReturn — "İadeyi bağla" (KR-H). 0177 links a CUSTOMER_RETURN posted elsewhere; it never posts a movement (D-3, D-27).</summary>
    [HttpPost("cmd/cases/{id}/return-links")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkReturn(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.ReturnsLink)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/return-links", "linkReturn", null, ct);
    }

    /// <summary>startEffectivenessCheck — "Etkinlik kontrolüne geç".</summary>
    [HttpPost("cmd/cases/{id}/effectiveness/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartEffectiveness(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.EffectivenessComplete)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/effectiveness/start", "startEffectivenessCheck", null, ct);
    }

    /// <summary>prepareCaseAction CompleteEffectiveness — step 1 of "Etkinlik kontrolünü tamamla".</summary>
    [HttpPost("cmd/cases/{id}/prepare-completion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareCompletion(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.EffectivenessComplete)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/pending-action", "prepareCaseAction", "CompleteEffectiveness", ct);
    }

    /// <summary>completeEffectivenessCheck — step 2 of "Etkinlik kontrolünü tamamla".</summary>
    [HttpPost("cmd/cases/{id}/effectiveness/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteEffectiveness(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.EffectivenessComplete)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/effectiveness/complete", "completeEffectivenessCheck", null, ct);
    }

    /// <summary>prepareCaseAction Close — step 1 of "Vakayı kapat".</summary>
    [HttpPost("cmd/cases/{id}/prepare-closure")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareClosure(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesClose)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/pending-action", "prepareCaseAction", "Close", ct);
    }

    /// <summary>closeCase — step 2 of "Vakayı kapat".</summary>
    [HttpPost("cmd/cases/{id}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseCase(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesClose)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/close", "closeCase", null, ct);
    }

    /// <summary>cancelCase — "Vakayı iptal et" (Draft / Kapsam onayı bekliyor; gerekçe ≥ 10).</summary>
    [HttpPost("cmd/cases/{id}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCase(string id, CancellationToken ct)
    {
        if (!_permissions.Has(RecallKeys.CasesCancel)) return Refuse(403, null);
        return await CaseCommandAsync(id, "/cancel", "cancelCase", null, ct);
    }

    /// <summary>
    /// One case command after its door's key check: the body as the page sent it (a prepare door puts ITS action in), the body's idempotency key
    /// as the Idempotency-Key header, the page's If-Match passed through. The service's answer (status + body) comes back unchanged.
    /// </summary>
    private async Task<IActionResult> CaseCommandAsync(string id, string suffix, string operation, string? action, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var caseId)) return Refuse(400, "INVALID_REQUEST");
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        JsonObject body;
        try { body = JsonNode.Parse(text) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { return Refuse(400, "INVALID_REQUEST"); }
        if (body["idempotencyKey"] is not JsonValue keyValue || !keyValue.TryGetValue<string>(out var key) || string.IsNullOrWhiteSpace(key)) return Refuse(400, "INVALID_REQUEST");
        if (action is not null) body["action"] = action;
        var reply = await SendAsync(HttpMethod.Post, $"{ApiBase}/cases/{caseId:D}{suffix}", body.ToJsonString(), key, ct, Header("If-Match"));
        if (!reply.IsSuccess) return Failed(operation, reply);
        return new ContentResult { StatusCode = reply.StatusCode, Content = reply.Body, ContentType = "application/json" };
    }

    /// <summary>Product / SKU / warehouse / location names for every object of a case answer that carries them (MasterLabelResolver).</summary>
    private async Task<bool> CaseLabelsAsync(JsonNode root, CancellationToken ct)
    {
        var objects = new List<JsonObject>();
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["item"] is JsonObject || o["warehouseId"] is not null || o["locationId"] is not null) objects.Add(o);
                    foreach (var child in o.Select(p => p.Value).ToList()) Walk(child);
                    break;
                case JsonArray a:
                    foreach (var child in a.ToList()) Walk(child);
                    break;
            }
        }

        Walk(root);
        if (objects.Count == 0) return true;
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return false;
        var labels = await MasterLabelResolver.ResolveAsync(ledger, objects.Select(o => (string?)o["item"]?["itemId"]),
            objects.Select(o => ((string?)o["item"]?["skuId"], (string?)o["item"]?["skuLevel"])),
            objects.SelectMany(o => new[] { (string?)o["warehouseId"], (string?)o["locationId"] }), ct);
        foreach (var o in objects)
        {
            if (o["item"] is JsonObject item)
            {
                o["itemLabel"] = labels.Product((string?)item["itemId"]) ?? (string?)item["itemId"];
                o["skuLabel"] = labels.Sku((string?)item["skuId"]);
            }

            if (o["warehouseId"] is not null) o["warehouseLabel"] = labels.Location((string?)o["warehouseId"]) ?? (string?)o["warehouseId"];
            if (o["locationId"] is not null) o["locationLabel"] = labels.Location((string?)o["locationId"]) ?? (string?)o["locationId"];
        }

        return labels.Available;
    }

    private static JsonArray Merge(JsonObject page)
    {
        var rows = new List<(int Position, JsonObject Row)>();
        foreach (var (name, kind) in new[] { ("items", "Case"), ("drillRows", "Drill") })
        {
            foreach (var row in (page[name] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                var copy = (JsonObject)row.DeepClone();
                copy["rowKind"] = kind;
                rows.Add(((int?)row["position"] ?? int.MaxValue, copy));
            }
        }

        return new JsonArray(rows.OrderBy(r => r.Position).Select(r => (JsonNode)r.Row).ToArray());
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>rootLotId (a GUID) + direction (Backward | Forward | Both, default Both) → the runTrace query; null = refused.</summary>
    private static string? TraceQuery(string? rootLotId, string? direction)
    {
        if (!Guid.TryParse(rootLotId, out var lot)) return null;
        var dir = string.IsNullOrWhiteSpace(direction) ? "Both" : direction;
        if (dir is not ("Backward" or "Forward" or "Both")) return null;
        return new QueryBuilder().Add("rootLotId", lot.ToString("D")).Add("direction", dir).ToString();
    }

    private async Task<Reply> SendAsync(HttpMethod method, string pathAndQuery, string? body, string? idempotencyKey, CancellationToken ct, string? ifMatch = null)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Reply.Error(401, "UNAUTHORIZED");
        var tenantId = ResolveTenantId(token);
        if (tenantId is null) return Reply.Error(401, "UNAUTHORIZED");

        Guid? selected = null;
        if (CompanySelection.TokenLegalEntity(User) is null)
        {
            selected = CompanySelection.SelectedCompany(Request, User);
            if (selected is null) return Reply.Error(403, InventoryGatewayClient.CompanyNotSelected);
        }

        var correlationId = Header(CorrelationHeaderName) ?? HttpContext.TraceIdentifier;
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
                "Recall proxy completed. method={Method} path={Path} status={StatusCode} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, pathAndQuery.Split('?')[0], (int)response.StatusCode, correlationId, stopwatch.ElapsedMilliseconds);
            return new Reply((int)response.StatusCode, text);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Recall proxy unreachable. method={Method} path={Path} correlation_id={CorrelationId}", method.Method, pathAndQuery.Split('?')[0], correlationId);
            return Reply.Error(503, "SERVICE_UNAVAILABLE");
        }
    }

    /// <summary>A file answer of the service — the BYTES as they came (the CSV's BOM included).</summary>
    private async Task<(int Status, byte[]? Bytes, string? FileName, string? Code)> SendFileAsync(string pathAndQuery, CancellationToken ct)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        var tenantId = string.IsNullOrWhiteSpace(token) ? null : ResolveTenantId(token);
        if (tenantId is null) return (401, null, null, "UNAUTHORIZED");
        Guid? selected = null;
        if (CompanySelection.TokenLegalEntity(User) is null)
        {
            selected = CompanySelection.SelectedCompany(Request, User);
            if (selected is null) return (403, null, null, InventoryGatewayClient.CompanyNotSelected);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _gatewayUrl + pathAndQuery);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            request.Headers.TryAddWithoutValidation(CorrelationHeaderName, HttpContext.TraceIdentifier);
            if (selected is not null) request.Headers.TryAddWithoutValidation(CompanySelection.LegalEntityHeader, selected.Value.ToString("D"));
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                return ((int)response.StatusCode, null, null, new Reply((int)response.StatusCode, text).Code);
            }

            return (200, await response.Content.ReadAsByteArrayAsync(ct),
                response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Recall file unreachable. path={Path}", pathAndQuery.Split('?')[0]);
            return (503, null, null, "SERVICE_UNAVAILABLE");
        }
    }

    /// <summary>Every <c>{ userId }</c> of the rows gets its display name (AuthService display-label, the caller's token) — the FU-0175-Q5-02 helper.</summary>
    private async Task PeopleAsync(CancellationToken ct, params JsonNode?[] roots)
    {
        var ids = Diten.Web.Models.Quarantine.QuarantinePeople.UserIds(roots);
        if (ids.Count == 0) return;
        var names = await Diten.Web.Models.Quarantine.QuarantinePeople.ResolveAsync(ids, async (path, c) =>
        {
            var reply = await SendAsync(HttpMethod.Get, path, null, null, c);
            return (reply.StatusCode, reply.Body);
        }, ct);
        Diten.Web.Models.Quarantine.QuarantinePeople.Fill(names, roots);
    }

    /// <summary>Product / SKU names of the rows (CaseRef.item, DrillCaseRow.item) — MasterLabelResolver.</summary>
    private async Task<MasterLabels> LabelsAsync(JsonArray rows, CancellationToken ct)
    {
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return MasterLabels.None;
        var list = rows.OfType<JsonObject>().ToList();
        return await MasterLabelResolver.ResolveAsync(ledger, list.Select(r => (string?)r["item"]?["itemId"]),
            list.Select(r => ((string?)r["item"]?["skuId"], (string?)r["item"]?["skuLevel"])), [], ct);
    }

    private static void Decorate(JsonArray rows, MasterLabels labels)
    {
        foreach (var row in rows.OfType<JsonObject>())
        {
            row["itemLabel"] = labels.Product((string?)row["item"]?["itemId"]) ?? (string?)row["item"]?["itemId"];
            row["skuLabel"] = labels.Sku((string?)row["item"]?["skuId"]);
        }
    }

    private (int Page, int Size) Paging()
    {
        var start = int.TryParse(Request.Query["start"], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
        var length = int.TryParse(Request.Query["length"], NumberStyles.None, CultureInfo.InvariantCulture, out var l) && l > 0 ? l : 10;
        var size = Math.Clamp(length, 1, MaxServicePageSize);
        return (start / size + 1, size);
    }

    private string? Search()
    {
        var term = Request.Query["search"].ToString().Trim();
        return term.Length == 0 ? null : term;
    }

    private string? Header(string name) => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()) ? value.ToString() : null;

    private IActionResult Json(JsonObject body) => Content(body.ToJsonString(), "application/json");

    private IActionResult Failed(string operation, Reply reply)
    {
        _logger.LogWarning("Recall {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Code);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Recall {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Error?.Code);
    }

    /// <summary>A refusal in the reader's language (Recall.Errors.{CODE}); the service's own code is kept.</summary>
    private IActionResult Refuse(int status, string? code)
    {
        var key = code is null ? (status is 401 or 403 ? "Recall.Errors.FORBIDDEN" : status >= 500 ? "Recall.Errors.SERVICE_UNAVAILABLE" : "Recall.Errors.GENERIC")
            : "Recall.Errors." + code;
        var message = _localizer[key];
        return new JsonResult(new { error = new { code = code ?? "ERROR", message = message.ResourceNotFound ? _localizer["Recall.Errors.GENERIC"].Value : message.Value } })
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

    private sealed record Reply(int StatusCode, string Body)
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

        public static Reply Error(int status, string code) => new(status, JsonSerializer.Serialize(new { error = new { code } }));
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
/// MOD-0177 pack §11.3 / §14 — the keys this Web reads for DISPLAY (UAS-001 §4: absent, not disabled). InventoryService's [HasPermission]
/// stays the authority. Action code → key, verbatim from the pack table (R6 rows).
/// </summary>
public static class RecallKeys
{
    public const string DashboardRead = "recall.dashboard.read";
    public const string TraceRun = "recall.trace.run";
    public const string TraceExport = "recall.trace.export";
    public const string CasesRead = "recall.cases.read";
    public const string CasesCreate = "recall.cases.create";

    // WP-0177-R7a — the case detail buttons (pack §11.3, §14.2)
    public const string CasesUpdate = "recall.cases.update";
    public const string CasesApprove = "recall.cases.approve";
    public const string CasesCancel = "recall.cases.cancel";
    public const string CasesClose = "recall.cases.close";
    public const string EffectivenessComplete = "recall.effectiveness.complete";
    public const string QuarantineRequestsCreate = "recall.quarantine-requests.create";
    public const string SegregationsConfirm = "recall.segregations.confirm";
    public const string NotificationsCreate = "recall.notifications.create";
    public const string ConsumptionsCreate = "recall.consumptions.create";
    public const string ReturnsLink = "recall.returns.link";

    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RECALL_TRACE_EXPORT"] = TraceExport,
        ["RECALL_CASE_OPEN"] = CasesCreate,
        ["RECALL_DASHBOARD_VIEW"] = DashboardRead, // the panel's links between the three pages
        ["RECALL_TRACE_VIEW"] = TraceRun,
        ["RECALL_CASES_VIEW"] = CasesRead,
        // R7a — Vaka Detayı, verbatim pack §11.3
        ["RECALL_SCOPE_MARK"] = CasesUpdate,
        ["RECALL_SCOPE_SUBMIT"] = CasesUpdate,
        ["RECALL_SCOPE_RETURN"] = CasesApprove,
        ["RECALL_SCOPE_APPROVE"] = CasesApprove,
        ["RECALL_SCOPE_REVISE"] = CasesUpdate,
        ["RECALL_QUARANTINE_REQUEST"] = QuarantineRequestsCreate,
        ["RECALL_SEGREGATION_CONFIRM"] = SegregationsConfirm,
        ["RECALL_CUSTOMER_NOTIFY"] = NotificationsCreate,
        ["RECALL_CONSUMPTION_DECLARE"] = ConsumptionsCreate,
        ["RECALL_RETURN_LINK"] = ReturnsLink,
        ["RECALL_EFFECTIVENESS_START"] = EffectivenessComplete,
        ["RECALL_EFFECTIVENESS_COMPLETE"] = EffectivenessComplete,
        ["RECALL_CASE_CLOSE"] = CasesClose,
        ["RECALL_CASE_CANCEL"] = CasesCancel
    };

    public static IReadOnlyDictionary<string, bool> For(IPermissionSnapshot permissions)
        => Actions.ToDictionary(a => a.Key, a => permissions.Has(a.Value), StringComparer.Ordinal);
}
