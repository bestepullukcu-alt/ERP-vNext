using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Traceability;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.CycleCounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0181 DL-4 — Sayımlar Panosu (CYCLECOUNTS) and the count sheet (COUNTSHEET, not in the menu) — pack §8.13, §11: tenant shell MVC controller,
// route /Warehouse/CycleCounts, views under Views/Warehouse/CycleCounts/. The browser talks only to this same-origin controller; it calls Gateway
// 5000 /api/warehouse/cycle-counts with CycleCountGatewayClient (cookie bearer, X-Tenant-Id, the company header, the page's Accept-Language). Every
// write is [ValidateAntiForgeryToken] and refused here without its key; InventoryService's [HasPermission] and state rules stay the authority
// (UAS-001 §4). A blind count's expected quantity never reaches this proxy before submit: the service leaves it out of the DTO (pack §8.2 rule 5).
[Authorize]
[Route("Warehouse/CycleCounts")]
public sealed class CycleCountsController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/CycleCounts";
    private const string Base = "/api/warehouse/cycle-counts";

    public static readonly IReadOnlyList<string> Statuses = ["Draft", "Released", "Counting", "Submitted", "Closed", "Cancelled"];
    public static readonly IReadOnlyList<string> CountTypes = ["Cycle", "Full"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<CycleCountsIndex> _localizer;
    private readonly ILogger<CycleCountsController> _logger;

    public CycleCountsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<CycleCountsIndex> localizer, ILogger<CycleCountsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: each view draws _AccessDenied (and nothing else) without cycle-count.counts.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Sheet/{countId}")]
    public IActionResult Sheet(string countId)
    {
        ViewData["CountId"] = WarehouseProxy.TryId(countId, out var id) ? id : string.Empty;
        return View($"{ViewRoot}/CountSheet.cshtml");
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    // List adapter: repo server-mode wire → listCycleCounts (status*, warehouseId, q; newest first).
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);
        var page = CycleCountProxy.Node(reply);
        var items = page?["items"] as JsonArray ?? [];
        var rows = items.OfType<JsonObject>().ToList();
        var labels = await LabelsAsync(rows.Select(r => CycleCountProxy.Text(r, "warehouseId")), [], ct);
        var people = await PeopleAsync(gateway, rows.SelectMany(r => new[] { CycleCountProxy.Text(r, "assignedTo"), CycleCountProxy.Text(r, "createdBy") }), ct);
        foreach (var row in rows) Decorate(row, labels, people);
        var total = page?["total"]?.GetValue<long>() ?? rows.Count;
        return Json(new { data = new { items = rows, total, filteredTotal = total }, draw = paging.Draw, namesAvailable = labels.Available, contractVersion = "v1" });
    }

    // Server export (BL-452): every matching count as CSV — the same filters and search as the list, at most WarehouseCsv.MaxRows (413 beyond).
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var rows = new List<JsonObject>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var body = CycleCountProxy.Node(reply);
            var total = body?["total"]?.GetValue<long>() ?? 0;
            if (total > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            var items = (body?["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            rows.AddRange(items);
            if (items.Count < 200 || rows.Count >= total) break;
        }

        var labels = await LabelsAsync(rows.Select(r => CycleCountProxy.Text(r, "warehouseId")), [], ct);
        var people = await PeopleAsync(gateway, rows.SelectMany(r => new[] { CycleCountProxy.Text(r, "assignedTo"), CycleCountProxy.Text(r, "createdBy") }), ct);
        foreach (var row in rows) Decorate(row, labels, people);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColNumber", "ColStatus", "ColWarehouse", "ColType", "ColPlannedDate", "ColAssignee", "ColLines", "ColCreated" }.Select(W);
        var lines = rows.Select(r => new[]
        {
            CycleCountProxy.Text(r, "number"), W("CycleCount.Status." + CycleCountProxy.Text(r, "status")), CycleCountProxy.Text(r, "warehouse"),
            W("CycleCount.Type." + CycleCountProxy.Text(r, "countType")), CycleCountProxy.Text(r, "plannedDate"), CycleCountProxy.Text(r, "assignedToName"),
            r["lineCount"]?.ToString(), CycleCountProxy.Text(r, "createdAt")
        });
        return File(WarehouseCsv.Write(header, lines), "text/csv; charset=utf-8", $"cycle-counts-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The list filters as the query tail (<c>&amp;…</c>), or null when one is outside the contract.</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = new List<string>();
        foreach (var status in source["status"].Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (!Statuses.Contains(status!, StringComparer.Ordinal)) return null;
            query.Add("status=" + status);
        }

        var warehouse = WarehouseProxy.Single(source["warehouseId"]);
        if (warehouse is not null)
        {
            if (!WarehouseProxy.TryId(warehouse, out var id)) return null;
            query.Add("warehouseId=" + id);
        }

        var search = WarehouseProxy.Single(source["search"]);
        if (search is not null) query.Add("q=" + Uri.EscapeDataString(search.Length > 100 ? search[..100] : search));
        return query.Count == 0 ? string.Empty : "&" + string.Join('&', query);
    }

    // KPI cards (AC-40): counted at read time by the service — never computed or stored here.
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? warehouseId, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        var query = string.Empty;
        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            if (!WarehouseProxy.TryId(warehouseId, out var id)) return Refuse(400, "VALIDATION_ERROR", null);
            query = "?warehouseId=" + id;
        }

        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}/dashboard{query}", ct);
        return reply.IsSuccess ? Content(reply.Body, "application/json") : Failed("dashboard", reply);
    }

    // QuickView / sheet header: the count (+ its ETag) and its timeline (the service projects a blind count's numbers out).
    [HttpGet("count/{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var countId)) return Refuse(404, "COUNT_NOT_FOUND", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}/{countId}", ct);
        if (!reply.IsSuccess) return Failed("get", reply);
        if (CycleCountProxy.Node(reply) is not JsonObject count) return Refuse(503, null, reply.CorrelationId);
        var events = await gateway.GetAsync($"{Base}/{countId}/events?page=1&pageSize=200", ct);
        var timeline = events.IsSuccess ? CycleCountProxy.Node(events)?["items"] as JsonArray : null;
        var labels = await LabelsAsync([CycleCountProxy.Text(count, "warehouseId"), .. (count["scopeLocationIds"] as JsonArray ?? []).Select(n => n?.GetValue<string>()),
            .. (count["scopeBinIds"] as JsonArray ?? []).Select(n => n?.GetValue<string>())], [], ct);
        var people = await PeopleAsync(gateway, new[] { "assignedTo", "createdBy", "submittedBy", "cancelledBy" }.Select(n => CycleCountProxy.Text(count, n))
            .Concat((timeline ?? []).OfType<JsonObject>().Select(e => CycleCountProxy.Text(e, "actorUserId"))), ct);
        Decorate(count, labels, people);
        count["scope"] = new JsonArray((count["scopeLocationIds"] as JsonArray ?? []).Select(n => (JsonNode?)JsonValue.Create(labels.Location(n?.GetValue<string>()) ?? n?.GetValue<string>())).ToArray());
        // DL-4 FIX-01: the scope bins by name for the count sheet's found-stock panel (a name the master cannot say is left out — never the id)
        count["scopeBins"] = new JsonArray((count["scopeBinIds"] as JsonArray ?? []).Select(n => n?.GetValue<string>()).OfType<string>()
            .Select(id => (JsonNode?)(labels.Location(id) is { } name ? new JsonObject { ["id"] = id, ["name"] = name } : new JsonObject { ["id"] = id })).ToArray());
        foreach (var e in (timeline ?? []).OfType<JsonObject>())
            if (CycleCountProxy.Text(e, "actorUserId") is { } actor) e["actorName"] = people.GetValueOrDefault(actor);
        return Json(new { count, etag = reply.ETag ?? $"W/\"{count["version"]}\"", timeline, timelineAvailable = timeline is not null, contractVersion = "v1" });
    }

    // The count sheet's lines (bin by bin): the service's lines with the bin and SKU names; a blind count's expected quantity is ABSENT in the
    // service's answer before submit (pack §8.2 rule 5), so it can never be drawn — hiding on screen is not the rule.
    [HttpGet("lines/{id}")]
    public async Task<IActionResult> Lines(string id, [FromQuery] int page, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var countId)) return Refuse(404, "COUNT_NOT_FOUND", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}/{countId}/lines?page={Math.Max(1, page)}&pageSize=200", ct);
        if (!reply.IsSuccess) return Failed("lines", reply);
        var body = CycleCountProxy.Node(reply) as JsonObject ?? new JsonObject();
        var lines = (body["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var labels = await LabelsAsync(lines.Select(l => CycleCountProxy.Text(l, "locationId")),
            lines.Select(l => (l["item"]?["itemId"]?.GetValue<string>(), l["item"]?["skuId"]?.GetValue<string>(), l["item"]?["skuLevel"]?.GetValue<string>())), ct);
        var lots = await LotNumbersAsync(lines, ct);
        foreach (var line in lines)
        {
            // DL-4 FIX-01 (CT live, owner Beste 2026-11-12): the sheet shows names, never an id — a name the master cannot say is left out and the
            // script writes the localized "could not be read" word
            if (labels.Location(CycleCountProxy.Text(line, "locationId")) is { } location) line["location"] = location;
            if ((labels.Sku(line["item"]?["skuId"]?.GetValue<string>()) ?? labels.Product(line["item"]?["itemId"]?.GetValue<string>())) is { } sku) line["sku"] = sku;
            if (CycleCountProxy.Text(line, "lotId") is { } lotId && lots.TryGetValue(lotId, out var lotNumber)) line["lotNumber"] = lotNumber;
            line["etag"] = $"W/\"{line["version"]}\"";
        }

        return Json(new { items = lines, total = body["total"]?.GetValue<long>() ?? lines.Count, namesAvailable = labels.Available });
    }

    /// <summary>
    /// DL-4 FIX-01: the lot NUMBER of every lot id on the page, read in batch from the lot register (MOD-0174 <c>GET /api/trace/lots?skuId&amp;skuLevel</c>,
    /// <c>trace.lots.read</c>) — ONE call per distinct SKU of the page (there is no by-ids read), never one per line. A reader without the key, or a lot
    /// the register does not answer, gets no number (the sheet then says so in words, never the id).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LotNumbersAsync(IReadOnlyList<JsonObject> lines, CancellationToken ct)
    {
        var wanted = lines.Select(l => CycleCountProxy.Text(l, "lotId")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0 || !_permissions.Has(TracePermissionKeys.LotsRead)) return new Dictionary<string, string>();
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return new Dictionary<string, string>();
        var skus = lines.Where(l => CycleCountProxy.Text(l, "lotId") is not null)
            .Select(l => (Sku: l["item"]?["skuId"]?.GetValue<string>(), Level: l["item"]?["skuLevel"]?.GetValue<string>()))
            .Where(s => WarehouseProxy.TryId(s.Sku, out _) && !string.IsNullOrWhiteSpace(s.Level)).Distinct().ToList();
        var replies = await Task.WhenAll(skus.Select(s =>
            trace.GetAsync($"/api/trace/lots?skuId={Uri.EscapeDataString(s.Sku!)}&skuLevel={Uri.EscapeDataString(s.Level!)}&page=1&pageSize=200", ct)));
        var numbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reply in replies.Where(r => r.IsSuccess))
        foreach (var lot in reply.Read<LotListDto>()?.Items ?? [])
            if (lot.LotId is { } id && wanted.Contains(id) && !string.IsNullOrWhiteSpace(lot.LotNumber)) numbers[id] = lot.LotNumber!;
        return numbers;
    }

    // DL-4 FIX-01 — the found-stock panel's lot field: the chosen SKU's registered lots by NUMBER (value = the lot id the service needs).
    [HttpGet("lots")]
    public async Task<IActionResult> Lots([FromQuery] string? skuId, [FromQuery] string? skuLevel, [FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRecord)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(skuId, out var sku) || string.IsNullOrWhiteSpace(skuLevel) || skuLevel.Length > 20) return Refuse(400, "VALIDATION_ERROR", null);
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Json(new { items = Array.Empty<object>(), available = false });
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim().Length > 100 ? q.Trim()[..100] : q.Trim());
        var reply = await trace.GetAsync($"/api/trace/lots?skuId={sku}&skuLevel={Uri.EscapeDataString(skuLevel.Trim())}&page=1&pageSize=50&sort=lotNumber{search}", ct);
        var lots = reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [];
        return Json(new { items = lots.Where(l => !string.IsNullOrWhiteSpace(l.LotNumber)).Select(l => new { value = l.LotId, text = l.LotNumber }), available = reply.IsSuccess });
    }

    // Warehouse filter / form field 1: the company's active warehouses (LOCATION master; never a hardcoded list).
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Form field 4 (scope) and the found-stock bin: the active stockable nodes under the warehouse.
    [HttpGet("nodes")]
    public async Task<IActionResult> Nodes([FromQuery] string? warehouseId, [FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(warehouseId, out var wid)) return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).StockableLocationsUnderAsync(wid, q, ct);
        return Json(new { items = result.Items.Take(50).Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Form field 5 (SKU filter): PRODUCT-MASTER SKUs — the picker value Level:skuId is reduced to the skuId the count stores.
    [HttpGet("skus")]
    public async Task<IActionResult> Skus([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new ProductPickerSource(inventory).SearchSkusAsync(q, ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value.Contains(':') ? o.Value[(o.Value.IndexOf(':') + 1)..] : o.Value, text = o.Text }), available = result.Available });
    }

    // Found stock: product, then its SKUs (value Level:skuId — the found line needs the level).
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRecord)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new ProductPickerSource(inventory).SearchProductsAsync(q, ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text, sub = o.Sub }), available = result.Available });
    }

    [HttpGet("product-skus")]
    public async Task<IActionResult> ProductSkus([FromQuery] string? itemId, [FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRecord)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(itemId, out var item)) return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new ProductPickerSource(inventory).SearchSkusOfProductAsync(item, q, ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Form field 7 (assignee): AuthService's user lookup — active users of the caller's tenant, label only.
    [HttpGet("people")]
    public async Task<IActionResult> People([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Refuse(403, null, null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync(QuarantinePeople.SearchPath(q), ct);
        return reply.IsSuccess
            ? Content(new JsonObject { ["items"] = QuarantinePeople.Options(reply.Body), ["available"] = true }.ToJsonString(), "application/json")
            : Json(new { items = Array.Empty<object>(), available = false });
    }

    // ── writes (pack §11; AC-41: one action per real button) ─────────────────────────────────────────────

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsCreate)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || FormBody(model) is not { } body) return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        body["idempotencyKey"] = key;
        return await WriteAsync("create", Base, body, key, null, "CycleCount.Saved.Created", ct);
    }

    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsUpdate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var countId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || FormBody(model) is not { } body)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        body["idempotencyKey"] = key;
        return await WriteAsync("update", $"{Base}/{countId}/update", body, key, model.IfMatch, "CycleCount.Saved.Updated", ct);
    }

    [HttpPost("release/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Release(string id, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => KeyedAsync("release", CycleCountPermissionKeys.CountsUpdate, id, model, false, c => $"{Base}/{c}/release", "CycleCount.Saved.Released", ct);

    [HttpPost("cancel/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string id, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => KeyedAsync("cancel", CycleCountPermissionKeys.CountsCancel, id, model, true, c => $"{Base}/{c}/cancel", "CycleCount.Saved.Cancelled", ct);

    [HttpPost("submit/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(string id, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => KeyedAsync("submit", CycleCountPermissionKeys.CountsRecord, id, model, false, c => $"{Base}/{c}/submit", "CycleCount.Saved.Submitted", ct);

    // "Toplu sil" (Draft only, G-0181-01 — a bulk soft delete with a reason): per-row outcomes, the screen reports each row.
    [HttpPost("bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete([FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsBulkDelete)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || model.Items is not { Count: > 0 and <= 200 }
            || model.Items.Any(i => !WarehouseProxy.TryId(i.Id, out _) || i.Version < 1))
            return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["reason"] = model.Reason?.Trim(),
            ["items"] = model.Items.Select(i => new { countId = Guid.Parse(i.Id!).ToString("D"), version = i.Version }).ToList()
        };
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/bulk-delete", body, key, null, ct);
        if (!reply.IsSuccess) return Failed("bulk-delete", reply);
        var results = CycleCountProxy.Node(reply)?["results"] as JsonArray ?? [];
        return Json(new { success = true, results, deleted = results.Count(r => r?["outcome"]?.GetValue<string>() == "Deleted") });
    }

    // The sheet: one line's counted quantity (If-Match = the LINE's ETag) — the quantity as typed (decimal string; the service owns the rule).
    [HttpPost("record/{countId}/{lineId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Record(string countId, string lineId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRecord)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(countId, out var count) || !WarehouseProxy.TryId(lineId, out var line) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey)
            || !WarehouseProxy.IsETag(model.IfMatch) || string.IsNullOrWhiteSpace(model.CountedQuantity))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["countedQuantity"] = model.CountedQuantity.Trim().Replace(',', '.') };
        if (!string.IsNullOrWhiteSpace(model.ScanRef)) body["scanRef"] = model.ScanRef.Trim();
        return await LineWriteAsync("record", $"{Base}/{count}/lines/{line}/count", body, key, model.IfMatch, ct);
    }

    // The sheet: found stock (a bin of the scope, a product's SKU, the lot when lot-controlled, the counted quantity).
    [HttpPost("add-line/{countId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(string countId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRecord)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(countId, out var count) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey)
            || !WarehouseProxy.TryId(model.LocationId, out var bin) || !WarehouseProxy.TryId(model.ItemId, out var item) || !WarehouseProxy.TryId(model.SkuId, out var sku)
            || string.IsNullOrWhiteSpace(model.SkuLevel) || string.IsNullOrWhiteSpace(model.CountedQuantity))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["locationId"] = bin, ["item"] = new { itemId = item, skuId = sku, skuLevel = model.SkuLevel.Trim() },
            ["countedQuantity"] = model.CountedQuantity.Trim().Replace(',', '.')
        };
        if (!string.IsNullOrWhiteSpace(model.LotId)) body["lotId"] = model.LotId.Trim();
        if (!string.IsNullOrWhiteSpace(model.ScanRef)) body["scanRef"] = model.ScanRef.Trim();
        return await LineWriteAsync("add-line", $"{Base}/{count}/lines", body, key, null, ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The Draft form (8 fields, pack §4.1) as the service's body; null when a member is outside the contract's shapes.</summary>
    public static Dictionary<string, object?>? FormBody(CycleCountWriteRequest model)
    {
        if (!WarehouseProxy.TryId(model.WarehouseId, out var warehouse) || !CountTypes.Contains(model.CountType ?? string.Empty, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(model.PlannedDate)) return null;
        var scope = new List<string>();
        foreach (var raw in model.ScopeLocationIds ?? [])
        {
            if (!WarehouseProxy.TryId(raw, out var id)) return null;
            scope.Add(id);
        }

        var skus = new List<string>();
        foreach (var raw in model.ScopeSkuIds ?? [])
        {
            if (!WarehouseProxy.TryId(raw, out var id)) return null;
            skus.Add(id);
        }

        var body = new Dictionary<string, object?>
        {
            ["warehouseId"] = warehouse, ["countType"] = model.CountType, ["blind"] = model.Blind,
            ["scopeLocationIds"] = model.CountType == "Full" ? new List<string>() : scope, ["scopeSkuIds"] = skus, ["plannedDate"] = model.PlannedDate.Trim()
        };
        if (!string.IsNullOrWhiteSpace(model.AssignedTo))
        {
            if (!WarehouseProxy.TryId(model.AssignedTo, out var assignee)) return null;
            body["assignedTo"] = assignee;
        }

        if (!string.IsNullOrWhiteSpace(model.Note)) body["note"] = model.Note.Trim();
        return body;
    }

    private async Task<IActionResult> KeyedAsync(string operation, string permission, string id, CycleCountWriteRequest? model, bool reasoned,
        Func<string, string> path, string savedKey, CancellationToken ct)
    {
        if (!_permissions.Has(permission)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var countId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (reasoned && !string.IsNullOrWhiteSpace(model.Reason)) body["reason"] = model.Reason.Trim(); // the service owns COUNT_REASON_REQUIRED
        return await WriteAsync(operation, path(countId), body, key, model.IfMatch, savedKey, ct);
    }

    private async Task<IActionResult> WriteAsync(string operation, string path, object body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var count = CycleCountProxy.Node(reply);
        return Json(new
        {
            success = true, id = CycleCountProxy.Text(count, "countId"), number = CycleCountProxy.Text(count, "number"), status = CycleCountProxy.Text(count, "status"),
            etag = reply.ETag ?? $"W/\"{count?["version"]}\"", message = _localizer[savedKey].Value
        });
    }

    private async Task<IActionResult> LineWriteAsync(string operation, string path, object body, string key, string? ifMatch, CancellationToken ct)
    {
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var line = CycleCountProxy.Node(reply) as JsonObject ?? new JsonObject();
        line["etag"] = reply.ETag ?? $"W/\"{line["version"]}\"";
        return Json(new { success = true, line, message = _localizer["CycleCount.Saved.Counted"].Value });
    }

    private void Decorate(JsonObject row, MasterLabels labels, IReadOnlyDictionary<string, string> people)
    {
        var warehouse = CycleCountProxy.Text(row, "warehouseId");
        row["warehouse"] = labels.Location(warehouse) ?? warehouse;
        foreach (var name in new[] { "assignedTo", "createdBy", "submittedBy", "cancelledBy" })
            if (CycleCountProxy.Text(row, name) is { } user) row[name + "Name"] = people.GetValueOrDefault(user) ?? user;
        row["etag"] = $"W/\"{row["version"]}\"";
    }

    private async Task<MasterLabels> LabelsAsync(IEnumerable<string?> locations, IEnumerable<(string? ItemId, string? SkuId, string? Level)> skus, CancellationToken ct)
    {
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return MasterLabels.None;
        var list = skus.ToList();
        return await MasterLabelResolver.ResolveAsync(inventory, list.Select(s => s.ItemId), list.Select(s => (s.SkuId, s.Level)), locations, ct);
    }

    private static async Task<IReadOnlyDictionary<string, string>> PeopleAsync(CycleCountGatewayClient gateway, IEnumerable<string?> ids, CancellationToken ct)
    {
        var distinct = ids.OfType<string>().Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<string, string>();
        return await QuarantinePeople.ResolveAsync(distinct, async (path, token) =>
        {
            var reply = await gateway.GetAsync(path, token);
            return (reply.StatusCode, reply.Body);
        }, ct);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Cycle count {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId, reply.Error?.Details);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId, object? details = null) =>
        CycleCountProxy.Error(status, code, _localizer[CycleCountErrorMessages.KeyFor(code, status)].Value, correlationId, details);
}
