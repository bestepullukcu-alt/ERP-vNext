using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0176 F4 — Son Kullanma / FEFO tenant pages (pack §9, §11.1–§11.3, §21.3) + their same-origin doors. Tenant shell only: every
// route is /Expiry/**, never /Platform. The controller holds no expiry rule — every figure, severity and order is InventoryService's
// (FEFO-BUNDLE 1.1.0): the MOD-0175 QuarantineController pattern, copied:
//   • api/{**everything} → Gateway /api/expiry/{everything} (the caller's own token from the auth cookie, TenantId from the token, the
//     W-0173-02 company rule, correlation id, Accept-Language, Idempotency-Key passed through). Writes ("İstisna aç") carry the
//     antiforgery token. Bodies are never logged.
//   • list/{approaching|expired} → the server-mode DataTable doors (start / length / search → page / pageSize / q).
//   • data/{dashboard|fefo-order} → one call per page + display names (MasterLabelResolver).
//   • export/{approaching|expired} → the list as CSV under expiry.dashboard.read, no evidence log (CT-F14).
//   • lookups/{warehouses|skus} → the pickers of the filters (FEFO Sırası: the SKUs with lot stock in the warehouse).
// UAS-001 is the views' (display only); InventoryService's [HasPermission] stays the authority.
[Authorize]
[Route("Expiry")]
public sealed class ExpiryController : Controller
{
    public const string ApiBase = "/api/expiry";
    private const string ViewRoot = "~/Views/Expiry";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const int MaxServicePageSize = 200;
    private const int MaxExportRows = 2000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<Diten.Web.Views.Expiry.Expiry> _localizer;
    private readonly ILogger<ExpiryController> _logger;

    public ExpiryController(
        IHttpClientFactory httpClientFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<Diten.Web.Views.Expiry.Expiry> localizer,
        ILogger<ExpiryController> logger)
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

    [HttpGet("Approaching")]
    public IActionResult Approaching() => View($"{ViewRoot}/Approaching.cshtml");

    [HttpGet("Expired")]
    public IActionResult Expired() => View($"{ViewRoot}/Expired.cshtml");

    [HttpGet("FefoOrder")]
    public IActionResult FefoOrder() => View($"{ViewRoot}/FefoOrder.cshtml");

    // WP-0176-F5 (pack §11.1): İstisna Kutusu, İstisna Detayı (+ the decision drawer), Ayarlar, Kanıt Paketi.

    [HttpGet("Exceptions")]
    public IActionResult Exceptions() => View($"{ViewRoot}/Exceptions.cshtml");

    [HttpGet("Exceptions/Details/{id}")]
    public IActionResult ExceptionDetails(string id) => View($"{ViewRoot}/ExceptionDetails.cshtml", Guid.TryParse(id, out var g) ? g.ToString("D") : string.Empty);

    [HttpGet("Policy")]
    public IActionResult Policy() => View($"{ViewRoot}/Policy.cshtml");

    [HttpGet("Evidence")]
    public IActionResult Evidence() => View($"{ViewRoot}/Evidence.cshtml");

    // ── FEFO-BUNDLE proxy ────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api/{**everything}")]
    public Task<IActionResult> ProxyRead(string? everything) => ProxyAsync(everything);

    [HttpPost("api/{**everything}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ProxyWrite(string? everything) => ProxyAsync(everything);

    /// <summary>F5: updatePolicy is the contract's one PUT (If-Match + Idempotency-Key + antiforgery, like every write).</summary>
    [HttpPut("api/{**everything}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ProxyReplace(string? everything) => ProxyAsync(everything);

    // ── server-mode lists ───────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("list/approaching")]
    public async Task<IActionResult> ListApproaching(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/lots/approaching{ApproachingQuery(page, size)}", null, null, ct);
        return await ListAsync("listApproaching", reply, ct);
    }

    [HttpGet("list/expired")]
    public async Task<IActionResult> ListExpired(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/lots/expired{ExpiredQuery(page, size)}", null, null, ct);
        return await ListAsync("listExpired", reply, ct);
    }

    // ── one call per page ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>FEFO Paneli: the panel + names; the company horizons when the reader may read the policy; the "min. kalan raf ömrü altı
    /// giriş · son 30 gün" count when the reader may read exceptions (pack §21.2 card 4 — not a DashboardView member).</summary>
    [HttpGet("data/dashboard")]
    public async Task<IActionResult> DashboardData(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var query = new QueryBuilder().Add("warehouseId", Request.Query["warehouseId"]).Add("periodDays", Request.Query["periodDays"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/dashboard{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("getDashboard", reply);
        var panel = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var top = panel["topRisk"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(top, ct);
        Decorate(top, labels);

        JsonObject? policy = null;
        if (_permissions.Has(ExpiryKeys.PoliciesRead))
        {
            var p = await SendAsync(HttpMethod.Get, $"{ApiBase}/policy", null, null, ct);
            policy = p.IsSuccess ? JsonNode.Parse(p.Body) as JsonObject : null;
        }

        int? belowMin = null;
        if (_permissions.Has(ExpiryKeys.ExceptionsRead))
        {
            var cases = await SendAsync(HttpMethod.Get, $"{ApiBase}/exceptions?type=BELOW_MIN_REMAINING&sort=-openedAtUtc&page=1&pageSize=200", null, null, ct);
            if (cases.IsSuccess)
            {
                var since = DateTime.UtcNow.AddDays(-30);
                belowMin = ((JsonNode.Parse(cases.Body)?["items"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>()
                    .Count(c => DateTime.TryParse((string?)c["openedAtUtc"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var at) && at >= since);
            }
        }

        return Json(new JsonObject
        {
            ["panel"] = panel,
            ["policy"] = policy is null ? null : new JsonObject { ["approachingDays"] = policy["approachingDays"]?.DeepClone(), ["criticalDays"] = policy["criticalDays"]?.DeepClone() },
            ["belowMinLast30"] = belowMin,
            ["labelsAvailable"] = labels.Available
        });
    }

    /// <summary>FEFO Sırası: getFefoOrder (read-only, KF-F) + names.</summary>
    [HttpGet("data/fefo-order")]
    public async Task<IActionResult> FefoOrderData(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.FefoOrderRead)) return Refuse(403, null);
        var query = new QueryBuilder()
            .Add("warehouseId", Request.Query["warehouseId"]).Add("itemId", Request.Query["itemId"]).Add("skuId", Request.Query["skuId"])
            .Add("skuLevel", Request.Query["skuLevel"]).Add("quantity", Request.Query["quantity"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/fefo-order{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("getFefoOrder", reply);
        var order = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        return Json(new JsonObject { ["order"] = order });
    }

    // ── CSV (pack §11.3 EXPIRY_LIST_EXPORT, CT-F14: under expiry.dashboard.read, not an evidence export) ───────────

    [HttpGet("export/{list}")]
    public async Task<IActionResult> Export(string list, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        if (list is not ("approaching" or "expired")) return Refuse(404, null);
        var rows = new List<JsonObject>();
        for (var page = 1; rows.Count < MaxExportRows; page++)
        {
            var path = list == "approaching" ? $"{ApiBase}/lots/approaching{ApproachingQuery(page, MaxServicePageSize)}" : $"{ApiBase}/lots/expired{ExpiredQuery(page, MaxServicePageSize)}";
            var reply = await SendAsync(HttpMethod.Get, path, null, null, ct);
            if (!reply.IsSuccess) return Failed("export." + list, reply);
            var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
            var items = (root["items"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            rows.AddRange(items.Select(i => (JsonObject)i.DeepClone()));
            if (items.Count < MaxServicePageSize || rows.Count >= ((int?)root["total"] ?? 0)) break;
        }

        var array = new JsonArray(rows.Take(MaxExportRows).Select(r => (JsonNode)r).ToArray());
        var labels = await LabelsAsync(array, ct);
        Decorate(array, labels);
        var expired = list == "expired";
        var header = new[]
        {
            T("Expiry.Col.Product"), "SKU", T("Expiry.Col.LotNo"), T("Expiry.Col.Warehouse"), T("Expiry.Col.Expiry"),
            expired ? T("Expiry.Col.SinceExpiry") : T("Expiry.Col.DaysLeft"), T("Expiry.Col.Available"), T("Expiry.Col.Held"), T("Expiry.Col.Unit"), T("Expiry.Col.Status")
        };
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', header.Select(Cell)));
        foreach (var r in array.OfType<JsonObject>())
        {
            csv.AppendLine(string.Join(';', new[]
            {
                (string?)r["itemLabel"], (string?)r["skuLabel"] ?? (string?)r["item"]?["skuId"], (string?)r["lotNumber"], (string?)r["warehouseLabel"],
                (string?)r["expiryDate"], (expired ? r["daysSinceExpiry"] : r["daysToExpiry"])?.ToString(), r["onHand"]?["available"]?.ToString(),
                r["onHand"]?["held"]?.ToString(), (string?)r["onHand"]?["baseUomId"], T("Expiry.Severity." + (string?)r["severity"])
            }.Select(Cell)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"expiry-{list}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── pickers ──────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? warehouseId, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead) && !_permissions.Has(ExpiryKeys.FefoOrderRead)) return Refuse(403, null);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return Refuse(401, null);
        switch (kind)
        {
            case "warehouses":
            {
                var reply = await ledger.GetAsync("/api/locations?type=Warehouse", ct);
                // FIX-LIVE1: LOCATION rows are read by LocationRef.locationId (location.openapi.yaml), never by "id".
                return reply.IsSuccess
                    ? Json(new { items = (LocationOptions.Parse(reply.Body) ?? []).Select(o => new { value = o.Value, text = o.Text }) })
                    : Failed("lookups.warehouses", reply);
            }
            case "skus":
            {
                // the SKUs with lot stock in the warehouse (the FEFO order is per SKU + warehouse, pack §8.2)
                if (!Guid.TryParse(warehouseId, out var wh)) return Refuse(400, "INVALID_REQUEST");
                var reply = await ledger.GetAsync($"/api/inventory/balance?warehouseId={wh:D}&page=1&pageSize=200", ct);
                if (!reply.IsSuccess) return Failed("lookups.skus", reply);
                var rows = (JsonNode.Parse(reply.Body)?["rows"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                    .Where(r => !string.IsNullOrEmpty((string?)r["lotId"])).ToList();
                var skus = rows.GroupBy(r => ((string?)r["itemId"], (string?)r["skuId"], (string?)r["skuLevel"])).Select(g => g.Key).ToList();
                var labels = await MasterLabelResolver.ResolveAsync(ledger, skus.Select(s => s.Item1), skus.Select(s => (s.Item2, s.Item3)), [], ct);
                return Json(new
                {
                    items = skus.Select(s => new
                    {
                        itemId = s.Item1, skuId = s.Item2, skuLevel = s.Item3,
                        text = string.Join(" · ", new[] { labels.Sku(s.Item2), labels.Product(s.Item1) ?? s.Item1 }.Where(x => !string.IsNullOrWhiteSpace(x)))
                    }),
                    labelsAvailable = labels.Available
                });
            }
            default:
                return Refuse(404, null);
        }
    }

    // ── F5 doors: İstisna Kutusu / Detayı, Ayarlar, Kanıt Paketi ──────────────────────────────────────────────────

    /// <summary>İstisna Kutusu — listExceptions as a server-mode DataTable door (+ names of products, warehouses and assignees).</summary>
    [HttpGet("list/exceptions")]
    public async Task<IActionResult> ListExceptions(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.ExceptionsRead)) return Refuse(403, null);
        var (page, size) = Paging();
        // the inbox tabs (mockup): Tümü · Açık + İnceleniyor · SLA geçti · Sonuçlanan — a status filter of the page wins over its tab
        var tab = Request.Query["tab"].ToString();
        var statuses = StatusFilter().ToList();
        if (statuses.Count == 0)
            statuses = tab switch
            {
                "active" or "late" => ["Open", "InReview"],
                "done" => ["Accepted", "Fixed", "Closed", "Cancelled"],
                _ => []
            };
        var late = tab == "late" ? "true" : Request.Query["late"].ToString();
        var query = new QueryBuilder().Add("page", page).Add("pageSize", size).Add("q", Search()).Add("type", Request.Query["type"])
            .Add("late", late).Add("warehouseId", Request.Query["warehouseId"]).Add("assignedToMe", Request.Query["assignedToMe"])
            .Add("sort", Request.Query["sort"].ToString() is { Length: > 0 } sort ? sort : "-openedAtUtc").ToString();
        foreach (var status in statuses) query += (query.Length == 0 ? "?" : "&") + "status=" + Uri.EscapeDataString(status);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/exceptions{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("listExceptions", reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var items = root["items"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(items, ct);
        Decorate(items, labels);
        await PeopleAsync(ct, items);
        var total = (int?)root["total"] ?? items.Count;
        var data = new JsonObject { ["items"] = items.DeepClone(), ["total"] = total, ["filteredTotal"] = total };
        return Json(new JsonObject { ["data"] = data, ["labelsAvailable"] = labels.Available });
    }

    /// <summary>İstisna Detayı — getException (+ the ETag the page writes with) + names of the product, warehouse and every person.</summary>
    [HttpGet("data/exception/{id}")]
    public async Task<IActionResult> ExceptionData(string id, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.ExceptionsRead)) return Refuse(403, null);
        if (!Guid.TryParse(id, out var exceptionId)) return Refuse(404, "EXPIRY_EXCEPTION_NOT_FOUND");
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/exceptions/{exceptionId:D}", null, null, ct);
        if (!reply.IsSuccess) return Failed("getException", reply);
        var record = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var rows = new JsonArray(record.DeepClone());
        var labels = await LabelsAsync(rows, ct);
        Decorate(rows, labels);
        var decorated = (JsonObject)rows[0]!;
        await PeopleAsync(ct, decorated);
        if (reply.ETag is not null) Response.Headers.ETag = reply.ETag;
        return Json(new JsonObject { ["exception"] = decorated.DeepClone(), ["etag"] = reply.ETag, ["labelsAvailable"] = labels.Available });
    }

    /// <summary>Ayarlar — getPolicy (+ the ETag of the save) and the change history with the names of who changed what.</summary>
    [HttpGet("data/policy")]
    public async Task<IActionResult> PolicyData(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.PoliciesRead)) return Refuse(403, null);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/policy", null, null, ct);
        if (!reply.IsSuccess) return Failed("getPolicy", reply);
        var history = await SendAsync(HttpMethod.Get,
            $"{ApiBase}/policy/history{new QueryBuilder().Add("page", Request.Query["page"]).Add("pageSize", Request.Query["pageSize"])}", null, null, ct);
        var policy = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var page = history.IsSuccess ? JsonNode.Parse(history.Body) as JsonObject : null;
        await PeopleAsync(ct, policy, page);
        return Json(new JsonObject { ["policy"] = policy.DeepClone(), ["etag"] = reply.ETag, ["history"] = page?.DeepClone() });
    }

    /// <summary>
    /// Ayarlar · "Ürün bazında görünüm" (read-only, MDM is the owner): the SKUs that hold lot stock in the company (at most 50) with
    /// PRODUCT-MASTER getSku's <c>shelfLifeDays</c> / <c>retestDays</c> / <c>minRemainingShelfLifeDays</c>; a missing value is shown
    /// "Tanımsız" (the rule is not applied — KF-C). Corrected in MDM, never here.
    /// </summary>
    [HttpGet("data/policy/products")]
    public async Task<IActionResult> PolicyProducts(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.PoliciesRead)) return Refuse(403, null);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return Refuse(401, null);
        var balance = await ledger.GetAsync("/api/inventory/balance?page=1&pageSize=200", ct);
        if (!balance.IsSuccess) return Failed("data.policy.products", balance);
        var skus = (JsonNode.Parse(balance.Body)?["rows"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Where(r => !string.IsNullOrEmpty((string?)r["lotId"]))
            .Select(r => ((string?)r["itemId"], (string?)r["skuId"], (string?)r["skuLevel"]))
            .Where(s => s.Item2 is not null && s.Item3 is not null).Distinct().Take(50).ToList();
        var labels = await MasterLabelResolver.ResolveAsync(ledger, skus.Select(s => s.Item1), skus.Select(s => (s.Item2, s.Item3)), [], ct);
        var items = new JsonArray();
        var available = labels.Available;
        foreach (var (itemId, skuId, level) in skus)
        {
            var reply = await ledger.GetAsync($"/api/product-master/skus/{Uri.EscapeDataString(skuId!)}?level={Uri.EscapeDataString(level!)}", ct);
            JsonNode? sku = null;
            if (reply.IsSuccess) sku = JsonNode.Parse(reply.Body);
            else available = false;
            var data = sku?["data"] ?? sku;
            items.Add(new JsonObject
            {
                ["itemId"] = itemId, ["skuId"] = skuId, ["skuLevel"] = level,
                ["itemLabel"] = labels.Product(itemId) ?? itemId, ["skuLabel"] = labels.Sku(skuId),
                ["read"] = reply.IsSuccess,
                ["shelfLifeDays"] = data?["shelfLifeDays"]?.DeepClone(),
                ["retestDays"] = data?["retestDays"]?.DeepClone(),
                ["minRemainingShelfLifeDays"] = data?["minRemainingShelfLifeDays"]?.DeepClone()
            });
        }

        return Json(new JsonObject { ["items"] = items, ["available"] = available });
    }

    /// <summary>Kanıt Paketi — previewEvidence with the names of the products / warehouses of its two case sections and of the policy editors.</summary>
    [HttpGet("data/evidence/preview")]
    public async Task<IActionResult> EvidencePreview(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.EvidenceExport)) return Refuse(403, null);
        var query = new QueryBuilder().Add("preset", Request.Query["preset"]).Add("from", Request.Query["from"]).Add("to", Request.Query["to"]).ToString();
        foreach (var (name, values) in new[] { ("warehouseIds", Request.Query["warehouseIds"]), ("types", Request.Query["types"]) })
            foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
                query += (query.Length == 0 ? "?" : "&") + name + "=" + Uri.EscapeDataString(value!);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/evidence/preview{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("previewEvidence", reply);
        var preview = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var cases = new JsonArray();
        foreach (var section in new[] { "exceptions", "fefoBypasses" })
            foreach (var row in (preview[section] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                cases.Add(row.DeepClone());
        var labels = await LabelsAsync(cases, ct);
        foreach (var section in new[] { "exceptions", "fefoBypasses" })
            if (preview[section] is JsonArray rows) Decorate(rows, labels);
        await PeopleAsync(ct, preview["policyChanges"]);
        return Json(new JsonObject { ["preview"] = preview.DeepClone(), ["labelsAvailable"] = labels.Available });
    }

    /// <summary>The export log with the exporter's name (the hash is shown shortened; the full value stays copyable on the page).</summary>
    [HttpGet("data/evidence/exports")]
    public async Task<IActionResult> EvidenceExports(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.EvidenceExport)) return Refuse(403, null);
        var reply = await SendAsync(HttpMethod.Get,
            $"{ApiBase}/evidence/exports{new QueryBuilder().Add("page", Request.Query["page"]).Add("pageSize", Request.Query["pageSize"])}", null, null, ct);
        if (!reply.IsSuccess) return Failed("listEvidenceExports", reply);
        var page = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        await PeopleAsync(ct, page);
        return Json(page);
    }

    /// <summary>getEvidenceExportFile — the STORED bytes, passed through unchanged (the hash in the log is of exactly these bytes).</summary>
    [HttpGet("evidence/file/{exportId}")]
    public async Task<IActionResult> EvidenceFile(string exportId, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.EvidenceExport)) return Refuse(403, null);
        if (!Guid.TryParse(exportId, out var id)) return Refuse(404, "EXPIRY_EVIDENCE_EXPORT_NOT_FOUND");
        var file = await SendFileAsync($"{ApiBase}/evidence/exports/{id:D}/file", ct);
        if (file.Bytes is null) return Refuse(file.Status == 0 ? 503 : file.Status, file.Code);
        return File(file.Bytes, file.ContentType ?? "application/octet-stream", file.FileName ?? $"expiry-evidence-{id:N}");
    }

    /// <summary>
    /// WP-0176-F5 bands (pack §8.3; the movement form and the lot forms of MOD-0173 / MOD-0174): what a band needs to SAY before the post —
    /// the SKU's shelf-life days (PRODUCT-MASTER getSku), the company's modes when the reader may read the policy, and the FEFO order of the
    /// warehouse when the reader may read it. Informational only: the posting / createLot hooks decide (their 422s are the authority).
    /// </summary>
    [HttpGet("band/context")]
    public async Task<IActionResult> BandContext([FromQuery] string? itemId, [FromQuery] string? skuId, [FromQuery] string? skuLevel,
        [FromQuery] string? warehouseId, CancellationToken ct)
    {
        if (!_permissions.Has(Diten.Web.Models.Inventory.InventoryPermissionKeys.CreateMovements) && !_permissions.Has(Diten.Web.Models.Traceability.TracePermissionKeys.LotsCreate)
            && !_permissions.Has(ExpiryKeys.DashboardRead))
            return Refuse(403, null);
        if (!Guid.TryParse(skuId, out var sku) || string.IsNullOrWhiteSpace(skuLevel)) return Refuse(400, "INVALID_REQUEST");
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return Refuse(401, null);

        var skuReply = await ledger.GetAsync($"/api/product-master/skus/{sku:D}?level={Uri.EscapeDataString(skuLevel)}", ct);
        var skuNode = skuReply.IsSuccess ? JsonNode.Parse(skuReply.Body) : null;
        skuNode = skuNode?["data"] ?? skuNode;
        var policy = await SendAsync(HttpMethod.Get, $"{ApiBase}/policy", null, null, ct);
        var policyNode = policy.IsSuccess ? JsonNode.Parse(policy.Body) : null;
        JsonNode? order = null;
        if (Guid.TryParse(warehouseId, out var warehouse) && Guid.TryParse(itemId, out var item))
        {
            var reply = await SendAsync(HttpMethod.Get,
                $"{ApiBase}/fefo-order{new QueryBuilder().Add("warehouseId", warehouse.ToString("D")).Add("itemId", item.ToString("D")).Add("skuId", sku.ToString("D")).Add("skuLevel", skuLevel)}",
                null, null, ct);
            order = reply.IsSuccess ? JsonNode.Parse(reply.Body) : null;
        }

        return Json(new JsonObject
        {
            ["sku"] = skuNode is null ? null : new JsonObject
            {
                ["shelfLifeDays"] = skuNode["shelfLifeDays"]?.DeepClone(),
                ["retestDays"] = skuNode["retestDays"]?.DeepClone(),
                ["minRemainingShelfLifeDays"] = skuNode["minRemainingShelfLifeDays"]?.DeepClone()
            },
            ["policy"] = policyNode is null ? null : new JsonObject
            {
                ["fefoOutboundMode"] = policyNode["fefoOutboundMode"]?.DeepClone(),
                ["belowMinRemainingMode"] = policyNode["belowMinRemainingMode"]?.DeepClone(),
                ["dateOverrideMode"] = policyNode["dateOverrideMode"]?.DeepClone()
            },
            ["order"] = order?.DeepClone()
        });
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private IEnumerable<string> StatusFilter() => Request.Query["status"].Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!);

    /// <summary>Every <c>{ userId }</c> of the trees gets its display name (AuthService display-label, the caller's token) — FU-0175-Q5-02.</summary>
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

    private async Task<(int Status, byte[]? Bytes, string? ContentType, string? FileName, string? Code)> SendFileAsync(string path, CancellationToken ct)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        var tenantId = string.IsNullOrWhiteSpace(token) ? null : ResolveTenantId(token);
        if (tenantId is null) return (401, null, null, null, "UNAUTHORIZED");
        Guid? selected = null;
        if (CompanySelection.TokenLegalEntity(User) is null)
        {
            selected = CompanySelection.SelectedCompany(Request, User);
            if (selected is null) return (403, null, null, null, InventoryGatewayClient.CompanyNotSelected);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _gatewayUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            request.Headers.TryAddWithoutValidation(CorrelationHeaderName, HttpContext.TraceIdentifier);
            if (selected is not null) request.Headers.TryAddWithoutValidation(CompanySelection.LegalEntityHeader, selected.Value.ToString("D"));
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                return ((int)response.StatusCode, null, null, null, new Reply((int)response.StatusCode, text, null).Code);
            }

            return (200, await response.Content.ReadAsByteArrayAsync(ct), response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Expiry evidence file unreachable. path={Path}", path);
            return (503, null, null, null, "SERVICE_UNAVAILABLE");
        }
    }

    private string ApproachingQuery(int page, int size)
        => new QueryBuilder().Add("page", page).Add("pageSize", size).Add("q", Search())
            .Add("warehouseId", Request.Query["warehouseId"]).Add("severity", Request.Query["severity"]).Add("horizonDays", Request.Query["horizonDays"])
            .Add("includeHeld", Request.Query["includeHeld"]).Add("sort", Request.Query["sort"]).ToString();

    private string ExpiredQuery(int page, int size)
        => new QueryBuilder().Add("page", page).Add("pageSize", size).Add("warehouseId", Request.Query["warehouseId"])
            .Add("minDaysSinceExpiry", Request.Query["minDaysSinceExpiry"]).ToString();

    private async Task<IActionResult> ListAsync(string operation, Reply reply, CancellationToken ct)
    {
        if (!reply.IsSuccess) return Failed(operation, reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var items = root["items"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(items, ct);
        Decorate(items, labels);
        var total = (int?)root["total"] ?? items.Count;
        var data = new JsonObject { ["items"] = items.DeepClone(), ["total"] = total, ["filteredTotal"] = total };
        return Content(new JsonObject { ["data"] = data, ["labelsAvailable"] = labels.Available }.ToJsonString(), "application/json");
    }

    private async Task<IActionResult> ProxyAsync(string? everything)
    {
        var path = string.IsNullOrWhiteSpace(everything) ? ApiBase : $"{ApiBase}/{everything.TrimStart('/')}";
        string? body = null;
        if (!HttpMethods.IsGet(Request.Method) && Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var reply = await SendAsync(new HttpMethod(Request.Method), path + Request.QueryString, body, Header("Idempotency-Key"), HttpContext.RequestAborted);
        if (reply.ETag is not null) Response.Headers.ETag = reply.ETag;
        return new ContentResult { StatusCode = reply.StatusCode == 0 ? 503 : reply.StatusCode, Content = reply.Body, ContentType = "application/json" };
    }

    private async Task<Reply> SendAsync(HttpMethod method, string pathAndQuery, string? body, string? idempotencyKey, CancellationToken ct)
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
            // F5: the version of a case / the policy travels with every state change (FEFO-BUNDLE §1, 409 EXPIRY_CONFLICT when stale)
            if (Header("If-Match") is { } ifMatch) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            _logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "Expiry proxy completed. method={Method} path={Path} status={StatusCode} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, pathAndQuery.Split('?')[0], (int)response.StatusCode, correlationId, stopwatch.ElapsedMilliseconds);
            return new Reply((int)response.StatusCode, text, response.Headers.ETag?.ToString());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Expiry proxy unreachable. method={Method} path={Path} correlation_id={CorrelationId}", method.Method, pathAndQuery.Split('?')[0], correlationId);
            return Reply.Error(503, "SERVICE_UNAVAILABLE");
        }
    }

    /// <summary>Display names of the ids on this page (products, SKUs, warehouses) — MasterLabelResolver.</summary>
    private async Task<MasterLabels> LabelsAsync(JsonArray rows, CancellationToken ct)
    {
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return MasterLabels.None;
        var list = rows.OfType<JsonObject>().ToList();
        return await MasterLabelResolver.ResolveAsync(ledger, list.Select(r => (string?)r["item"]?["itemId"]),
            list.Select(r => ((string?)r["item"]?["skuId"], (string?)r["item"]?["skuLevel"])), list.Select(r => (string?)r["warehouse"]?["id"]), ct);
    }

    /// <summary>Adds itemLabel / skuLabel / warehouseLabel next to the ids (a missing name stays the raw id or the warehouse code).</summary>
    private static void Decorate(JsonArray rows, MasterLabels labels)
    {
        foreach (var row in rows.OfType<JsonObject>())
        {
            var item = row["item"];
            row["itemLabel"] = labels.Product((string?)item?["itemId"]) ?? (string?)item?["itemId"];
            row["skuLabel"] = labels.Sku((string?)item?["skuId"]);
            var warehouse = row["warehouse"];
            var codeName = string.Join(" · ", new[] { (string?)warehouse?["code"], (string?)warehouse?["name"] }.Where(x => !string.IsNullOrWhiteSpace(x)));
            row["warehouseLabel"] = labels.Location((string?)warehouse?["id"]) ?? (codeName.Length > 0 ? codeName : (string?)warehouse?["id"]);
        }
    }

    private static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text; // CSV formula injection guard
        return text.Contains(';') || text.Contains('"') || text.Contains('\n') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private string T(string key) => _localizer[key].Value;

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
        _logger.LogWarning("Expiry {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Code);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Expiry {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Error?.Code);
    }

    /// <summary>A refusal in the reader's language (Expiry.Errors.{CODE}); the service's own code is kept.</summary>
    private IActionResult Refuse(int status, string? code)
    {
        var key = code is null ? (status is 401 or 403 ? "Expiry.Errors.FORBIDDEN" : status >= 500 ? "Expiry.Errors.SERVICE_UNAVAILABLE" : "Expiry.Errors.GENERIC")
            : "Expiry.Errors." + code;
        var message = _localizer[key];
        return new JsonResult(new { error = new { code = code ?? "ERROR", message = message.ResourceNotFound ? _localizer["Expiry.Errors.GENERIC"].Value : message.Value } })
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
/// MOD-0176 pack §11.3 / §14 — the keys this Web reads for DISPLAY (UAS-001 §4: absent, not disabled). InventoryService's
/// [HasPermission] stays the authority. Action code → key, verbatim from the pack table; "Karantinaya al" is MOD-0175's key (a link).
/// </summary>
public static class ExpiryKeys
{
    public const string DashboardRead = "expiry.dashboard.read";
    public const string FefoOrderRead = "expiry.fefo-order.read";
    public const string ExceptionsRead = "expiry.exceptions.read";
    public const string ExceptionsCreate = "expiry.exceptions.create";
    public const string PoliciesRead = "expiry.policies.read";
    public const string QuarantineHoldsCreate = "quarantine.holds.create";
    // WP-0176-F5
    public const string ExceptionsReview = "expiry.exceptions.review";
    public const string ExceptionsComment = "expiry.exceptions.comment";
    public const string ExceptionsResolve = "expiry.exceptions.resolve";
    public const string ExceptionsClose = "expiry.exceptions.close";
    public const string PoliciesManage = "expiry.policies.manage";
    public const string EvidenceExport = "expiry.evidence.export";
    /// <summary>MOD-0022's key (the shared signature dialog) — display only: without it the drawer says signing is not possible.</summary>
    public const string ESignSign = "esign.signatures.sign";

    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["EXPIRY_EXCEPTION_OPEN"] = ExceptionsCreate,
        ["EXPIRY_FEFO_ORDER_VIEW"] = FefoOrderRead,
        ["EXPIRY_LIST_EXPORT"] = DashboardRead,
        ["EXPIRY_FEFO_ORDER_CALCULATE"] = FefoOrderRead,
        ["QUARANTINE_LINK"] = QuarantineHoldsCreate,
        ["EXPIRY_EXCEPTION_TAKE"] = ExceptionsReview,
        ["EXPIRY_EXCEPTION_COMMENT"] = ExceptionsComment,
        ["EXPIRY_EXCEPTION_RESOLVE"] = ExceptionsResolve,
        ["EXPIRY_EXCEPTION_CLOSE"] = ExceptionsClose,
        ["EXPIRY_POLICY_SAVE"] = PoliciesManage,
        ["EXPIRY_EVIDENCE_EXPORT"] = EvidenceExport,
        ["ESIGN_SIGN"] = ESignSign,
        ["EXPIRY_EXCEPTIONS_VIEW"] = ExceptionsRead // the inbox / detail are readable (a new case opens on its page)
    };

    public static IReadOnlyDictionary<string, bool> For(IPermissionSnapshot permissions)
        => Actions.ToDictionary(a => a.Key, a => permissions.Has(a.Value), StringComparer.Ordinal);
}
