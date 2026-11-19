using System.Text.Json;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.OutboundShipments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-6 — Outbound Sevkiyatlar (OUTBOUNDSHIPMENTS, pack §11.1 / §11.5; Golden Reference Compact): tenant shell MVC controller, route
// /Warehouse/OutboundShipments (+ /Create, /Edit/{id}, /Details/{id}), views under Views/Warehouse/OutboundShipments/. The browser talks only to
// this same-origin controller; it calls Gateway 5000 /api/warehouse (INT-WMS, DL-8) with WarehouseGatewayClient: cookie bearer, X-Tenant-Id, the
// W-0173-02 company header. Product / SKU / location names come from /api/product-master and /api/locations (read paths that never fail the page).
// Every data action checks ITS OWN key first (DL-3 CT F-1 lesson — WarehouseProxyActionKeyTests); every write is [ValidateAntiForgeryToken].
// InventoryService's [HasPermission] and the §8.18 state machine stay the authority (UAS-001 §4): the screen's menu (rules.js) only hides / disables.
[Authorize]
[Route("Warehouse/OutboundShipments")]
public sealed class OutboundShipmentsController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/OutboundShipments";
    private const string Base = "/api/warehouse/outbound-shipments";

    /// <summary>The six visible statuses (§8.18) — <c>Allocating</c> / <c>AllocationFailed</c> are internal and never listed.</summary>
    public static readonly IReadOnlyList<string> Statuses = ["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"];

    public static readonly IReadOnlyList<string> SkuLevels = ["Gsku", "Lsku", "FinishedGood", "Item"];

    /// <summary>The refusals whose <c>details</c> the screen shows (allocation line failures, FEFO deviations, item resolution).</summary>
    private static readonly string[] DetailedCodes = ["WMS_ALLOCATION_FAILED", "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "WMS_ITEM_NOT_RESOLVABLE"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<OutboundShipmentsIndex> _localizer;
    private readonly ILogger<OutboundShipmentsController> _logger;

    public OutboundShipmentsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<OutboundShipmentsIndex> localizer, ILogger<OutboundShipmentsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // ── pages (UAS-001: each view draws _AccessDenied — and nothing else — without its key; no redirect) ──────────

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult CreatePage() => View($"{ViewRoot}/Create.cshtml", new OutboundShipmentFormViewModel());

    [HttpGet("Edit/{id}")]
    public IActionResult EditPage(string id) =>
        View($"{ViewRoot}/Edit.cshtml", new OutboundShipmentFormViewModel { Id = WarehouseProxy.TryId(id, out var outboundId) ? outboundId : null });

    [HttpGet("Details/{id}")]
    public IActionResult DetailsPage(string id) =>
        View($"{ViewRoot}/Details.cshtml", new OutboundShipmentFormViewModel { Id = WarehouseProxy.TryId(id, out var outboundId) ? outboundId : null });

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    // List adapter: repo server-mode wire → searchOutboundShipments (status*, warehouseId, q; newest first). Not the frozen list.
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}/search?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsOutboundPageDto>() ?? new WmsOutboundPageDto();
        var rows = page.Items ?? [];
        var total = page.Total ?? rows.Count;
        return Json(new { data = new { items = rows.Select(Row), total, filteredTotal = total }, draw = paging.Draw, contractVersion = InventoryProxy.ContractVersion });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var rows = new List<WmsOutboundSummaryDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}/search?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsOutboundPageDto>() ?? new WmsOutboundPageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            rows.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || rows.Count >= (dto.Total ?? 0)) break;
        }

        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColNumber", "ColStatus", "ColWarehouse", "ColOrderRef", "ColShipTo", "ColLines", "ColCreated" }.Select(W);
        var lines = rows.Select(r => new[]
        {
            r.OutboundNumber, W("Warehouse.Outbound.Status." + r.Status), r.WarehouseCode ?? r.WarehouseId, r.OrderRef, r.ShipToName,
            r.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture), r.CreatedAtUtc
        });
        return File(WarehouseCsv.Write(header, lines), "text/csv; charset=utf-8", $"outbound-shipments-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The list filters as the WMS query tail (<c>&amp;…</c>, newest first), or null when one is outside the contract.</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = new List<string> { "orderBy=-createdAt" };
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
        return "&" + string.Join('&', query);
    }

    /// <summary>One list row: the summary + what the §8.18 menu needs (status, item level) — the menu itself is rules.js.</summary>
    public static object Row(WmsOutboundSummaryDto o) => new
    {
        id = o.OutboundId,
        number = o.OutboundNumber,
        o.Status,
        warehouse = o.WarehouseCode ?? o.WarehouseId,
        o.WarehouseId,
        o.OrderRef,
        shipTo = o.ShipToName,
        lines = o.LineCount,
        o.ItemLevel,
        o.CreatedAtUtc,
        version = o.Version,
        etag = $"W/\"{o.Version}\""
    };

    // Details / Edit: the outbound view (+ its ETag), its timeline (newest first) and the names of its ids.
    [HttpGet("outbound/{id}")]
    public async Task<IActionResult> Outbound(string id, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var outboundId)) return Refuse(404, "UNKNOWN_OUTBOUND", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}/{outboundId}/view", ct);
        if (!reply.IsSuccess) return Failed("view", reply);
        var view = reply.Read<WmsOutboundViewDto>();
        if (view is null) return Refuse(503, null, reply.CorrelationId);

        var events = await gateway.GetAsync($"{Base}/{outboundId}/events?page=1&pageSize=100", ct);
        var timeline = events.IsSuccess ? events.Read<WmsTaskEventPageDto>()?.Items ?? [] : null;
        var labels = await LabelsAsync(view, ct);
        return Json(new
        {
            outbound = Shape(view, labels),
            etag = reply.ETag ?? $"W/\"{view.Version}\"",
            timeline = timeline?.Select(e => new { e.Seq, e.Kind, e.AtUtc, e.System, actor = e.Actor is null ? null : new { e.Actor.UserId, e.Actor.Name } }),
            timelineAvailable = timeline is not null,
            namesAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    /// <summary>The outbound as the Details / Edit pages read it: its own fields, the names of its ids (raw id when a master could not say).</summary>
    public static object Shape(WmsOutboundViewDto o, MasterLabels labels) => new
    {
        id = o.OutboundId,
        number = o.OutboundNumber,
        o.Status,
        o.ItemLevel,
        warehouse = o.WarehouseCode ?? labels.Location(o.WarehouseId) ?? o.WarehouseId,
        o.WarehouseId,
        staging = o.StagingLocationCode ?? labels.Location(o.StagingLocationId) ?? o.StagingLocationId,
        o.StagingLocationId,
        o.OrderRef,
        shipTo = o.ShipTo,
        o.Note,
        o.ReadyAt,
        o.CreatedAtUtc,
        createdBy = o.CreatedBy?.Name ?? o.CreatedBy?.UserId,
        o.CancelReason,
        version = o.Version,
        lines = (o.Lines ?? []).Select(l => new
        {
            l.LineSeq,
            l.OrderLineId,
            sku = labels.Sku(l.Item?.SkuId) ?? labels.Product(l.Item?.ItemId) ?? l.Item?.SkuId,
            skuLevel = l.Item?.SkuLevel,
            l.Quantity,
            uom = l.UomId,
            lotOverride = l.LotOverride?.LotId,
            allocations = (l.Allocations ?? []).Select(a => new
            {
                a.AllocSeq, a.LotNumber, a.LotId, a.Quantity, a.FefoRank, a.FefoBypassReason, a.ReservationReleaseState,
                picked = a.PickedQuantity, packed = a.PackedQuantity,
                bins = (a.PlannedBins ?? []).Select(b => new { location = b.LocationCode ?? labels.Location(b.LocationId) ?? b.LocationId, b.Quantity })
            })
        }),
        pickTasks = (o.PickTasks ?? []).Select(t => new
        {
            id = t.TaskId, t.Number, t.Status, t.AllocSeq, from = t.FromLocationCode ?? labels.Location(t.FromLocationId) ?? t.FromLocationId, t.LotNumber,
            planned = t.PlannedQuantity, confirmed = t.ConfirmedQuantity
        }),
        packages = (o.Packages ?? []).Select(p => new { id = p.PackageId, number = p.PackageNumber, p.Status, p.WeightKg, p.DimensionsCm, contents = p.Contents ?? [] }),
        shipment = o.Shipment is null ? null : new { number = o.Shipment.ShipmentNumber ?? o.Shipment.ShipmentId, o.Shipment.Status, o.Shipment.ObservedAt },
        shortDispatch = (o.ShortDispatchUnresolved ?? []).Select(s => new { sku = labels.Sku(s.SkuId) ?? s.SkuId, s.Expected, s.Issued, s.Reason })
    };

    // Warehouse filter + form: the tenant company's active warehouses (LOCATION master; never a hardcoded list).
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Staging (§4.5, §8.17 step 1): optional — the service picks the warehouse's only Staging node; with several the reader names one of the
    // warehouse's stockable nodes and the service checks the role (WMS_TARGET_NOT_ALLOWED otherwise).
    [HttpGet("stagings")]
    public async Task<IActionResult> Stagings([FromQuery] string? warehouseId, [FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(warehouseId, out var wid)) return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).StockableLocationsUnderAsync(wid, q, ct);
        return Json(new { items = result.Items.Take(50).Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Line product (PRODUCT-MASTER searchStockableItems, Active only — MOD-0173 PM1 picker, used as is).
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new ProductPickerSource(inventory).SearchProductsAsync(q, ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text, sub = o.Sub }), available = result.Available });
    }

    // Line SKU of the chosen product → value "Level:skuId" (an Item is its own SKU, skuId = itemId — §8.24).
    [HttpGet("skus")]
    public async Task<IActionResult> Skus([FromQuery] string? itemId, [FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(itemId, out var item)) return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new ProductPickerSource(inventory).SearchSkusOfProductAsync(item, q, ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // The line's unit: the SKU's base unit (§8.17 step 1 — uomId = the SKU base unit, else 422 WMS_UOM_NOT_BASE).
    [HttpGet("sku-facts")]
    public async Task<IActionResult> SkuFacts([FromQuery] string? itemId, [FromQuery] string? skuId, [FromQuery] string? skuLevel, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(itemId, out var item) || !WarehouseProxy.TryId(skuId, out var sku) || !SkuLevels.Contains(skuLevel ?? string.Empty, StringComparer.Ordinal))
            return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var facts = await MovementFormSources.ProductFactsAsync(inventory, item, sku, skuLevel!, ct);
        return Json(new { available = facts.Available, baseUom = facts.BaseUomId, lotControlled = facts.LotControlled });
    }

    // ── writes (pack §11.3) ───────────────────────────────────────────────────────────────────────────────

    // "Tahsisi önizle" (§8.17 step 7): steps 1-4 without a write — lots in FEFO order, the allocatable quantity, a deviation's FEFO mode.
    [HttpPost("preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview([FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        if (model is null || CreateBody(model, requireShipTo: false) is not { } body) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/allocation-preview", body, null, null, ct);
        if (!reply.IsSuccess) return Failed("preview", reply);
        return Content(reply.Body, "application/json");
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCreate)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || CreateBody(model, requireShipTo: true) is not { } body)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        body["idempotencyKey"] = key;
        return await WriteAsync("create", HttpMethod.Post, Base, body, key, null, "Warehouse.Outbound.Saved.Created", ct);
    }

    // Edit (updateOutboundShipment, If-Match): the header only — order ref, ship-to, note; the lines / allocation are never members (400).
    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsUpdate)) return Refuse(403, null, null);
        var changes = model is null ? null : EditBody(model.Changes);
        if (!WarehouseProxy.TryId(id, out var outboundId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || changes is null || changes.Count == 0)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        changes["idempotencyKey"] = key;
        return await WriteAsync("update", HttpMethod.Put, $"{Base}/{outboundId}", changes, key, model.IfMatch, "Warehouse.Outbound.Saved.Updated", ct);
    }

    // "Sevke hazır" (§8.21): Packed → ReadyToShip; a 422 FEFO_BYPASS_REASON_REQUIRED comes back with its deviations and the same dialog asks a reason
    // per allocation (r22). An Item outbound answers 409 WMS_ITEM_LEVEL_NOT_PUBLISHABLE (§8.24) — the screen keeps the button disabled with its reason.
    [HttpPost("release/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(string id, [FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsRelease)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var outboundId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || model.FefoBypassReasons is { Count: > 100 } || (model.FefoBypassReasons ?? []).Any(r => r.AllocSeq < 1 || string.IsNullOrWhiteSpace(r.Reason)))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (model.FefoBypassReasons is { Count: > 0 } reasons)
            body["fefoBypassReasons"] = reasons.Select(r => new { allocSeq = r.AllocSeq, reason = r.Reason!.Trim() }).ToList(); // the service owns 10-1000
        return await WriteAsync("release", HttpMethod.Post, $"{Base}/{outboundId}/release", body, key, model.IfMatch, "Warehouse.Outbound.Saved.Released", ct);
    }

    // "Sevkiyatı kontrol et" (§8.21): reads MVP-6's shipments for this outbound; Shipped when one is dispatched, else unchanged with the reason.
    [HttpPost("sync/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(string id, [FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsUpdate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var outboundId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/{outboundId}/sync-dispatch", new { idempotencyKey = key }, key, model.IfMatch, ct);
        if (!reply.IsSuccess) return Failed("sync-dispatch", reply);
        var answer = reply.Read<WmsSyncDispatchDto>();
        var seen = answer?.DispatchSeen == true;
        return Json(new
        {
            success = true,
            dispatchSeen = seen,
            status = answer?.Outbound?.Status,
            etag = reply.ETag ?? (answer?.Outbound is null ? null : $"W/\"{answer.Outbound.Version}\""),
            message = _localizer[seen ? "Warehouse.Outbound.Saved.Shipped" : "Warehouse.Outbound.Saved.NotShippedYet"].Value
        });
    }

    // "İptal" (§8.22, gerekçe 10-1000, IsDangerous): Allocated / Picked / Packed; reservations released, staged stock put back.
    [HttpPost("cancel/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string id, [FromBody] OutboundWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.OutboundShipmentsCancel)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var outboundId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (!string.IsNullOrWhiteSpace(model.Reason)) body["reason"] = model.Reason.Trim(); // the service owns REASON_REQUIRED (10-1000)
        return await WriteAsync("cancel", HttpMethod.Post, $"{Base}/{outboundId}/cancel", body, key, model.IfMatch, "Warehouse.Outbound.Saved.Cancelled", ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The create / preview body (WMS draft §11): warehouse, optional Staging, order ref, ship-to (frozen Address members), note, 1-100 lines
    /// <c>{ orderLineId, item { itemId, skuId?, skuLevel }, uomId, quantity, lotOverride? }</c>; null when a member is outside the contract shape. The
    /// service decides every business rule (FEFO, Item resolution, base unit, mixing).
    /// </summary>
    public static Dictionary<string, object?>? CreateBody(OutboundWriteRequest model, bool requireShipTo)
    {
        if (!WarehouseProxy.TryId(model.WarehouseId, out var warehouse) || model.Lines is not { Count: > 0 and <= 100 }) return null;
        var body = new Dictionary<string, object?>(StringComparer.Ordinal) { ["warehouseId"] = warehouse };
        if (!string.IsNullOrWhiteSpace(model.StagingLocationId))
        {
            if (!WarehouseProxy.TryId(model.StagingLocationId, out var staging)) return null;
            body["stagingLocationId"] = staging;
        }

        if (!string.IsNullOrWhiteSpace(model.OrderRef)) body["orderRef"] = model.OrderRef.Trim();
        var shipTo = Address(model.ShipTo);
        if (shipTo is null && requireShipTo) return null;
        if (shipTo is not null) body["shipTo"] = shipTo;
        if (!string.IsNullOrWhiteSpace(model.Note)) body["note"] = model.Note.Trim();

        var lines = new List<object>();
        foreach (var line in model.Lines)
        {
            var quantity = WarehouseProxy.Quantity(line.Quantity);
            if (string.IsNullOrWhiteSpace(line.OrderLineId) || !WarehouseProxy.TryId(line.ItemId, out var itemId) || quantity is null
                || !SkuLevels.Contains(line.SkuLevel ?? string.Empty, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(line.UomId))
                return null;
            var item = new Dictionary<string, object?> { ["itemId"] = itemId, ["skuLevel"] = line.SkuLevel };
            if (!string.IsNullOrWhiteSpace(line.SkuId))
            {
                if (!WarehouseProxy.TryId(line.SkuId, out var skuId)) return null;
                item["skuId"] = skuId;
            }

            var entry = new Dictionary<string, object?> { ["orderLineId"] = line.OrderLineId.Trim(), ["item"] = item, ["uomId"] = line.UomId.Trim(), ["quantity"] = quantity };
            if (line.LotOverride is { } lot && !string.IsNullOrWhiteSpace(lot.LotId))
            {
                if (!WarehouseProxy.TryId(lot.LotId, out var lotId)) return null;
                var over = new Dictionary<string, object?> { ["lotId"] = lotId };
                if (!string.IsNullOrWhiteSpace(lot.FefoBypassReason)) over["fefoBypassReason"] = lot.FefoBypassReason.Trim();
                entry["lotOverride"] = over;
            }

            lines.Add(entry);
        }

        body["lines"] = lines;
        return body;
    }

    private static Dictionary<string, object?>? Address(OutboundAddressRequest? a)
    {
        if (a is null || string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Country)) return null;
        var address = new Dictionary<string, object?> { ["name"] = a.Name.Trim(), ["country"] = a.Country.Trim() };
        foreach (var (name, value) in new[] { ("city", a.City), ("postalCode", a.PostalCode), ("line1", a.Line1), ("line2", a.Line2) })
            if (!string.IsNullOrWhiteSpace(value)) address[name] = value.Trim();
        return address;
    }

    /// <summary>The PUT body: only <c>orderRef</c>, <c>shipTo</c>, <c>note</c> (a line or an allocation member is refused here, 400 at the service too).</summary>
    public static Dictionary<string, object?>? EditBody(JsonElement changes)
    {
        if (changes.ValueKind != JsonValueKind.Object) return null;
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in changes.EnumerateObject())
        {
            var value = member.Value;
            switch (member.Name)
            {
                case "orderRef" or "note" when value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.String:
                    body[member.Name] = value.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim();
                    break;
                case "shipTo" when value.ValueKind == JsonValueKind.Object:
                    var address = Address(value.Deserialize<OutboundAddressRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                    if (address is null) return null;
                    body[member.Name] = address;
                    break;
                default:
                    return null;
            }
        }

        return body;
    }

    private async Task<IActionResult> WriteAsync(string operation, HttpMethod method, string path, object body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(method, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var view = reply.Read<WmsOutboundViewDto>();
        return Json(new
        {
            success = true, id = view?.OutboundId, number = view?.OutboundNumber, status = view?.Status,
            etag = reply.ETag ?? (view is null ? null : $"W/\"{view.Version}\""), message = _localizer[savedKey].Value
        });
    }

    private async Task<MasterLabels> LabelsAsync(WmsOutboundViewDto view, CancellationToken ct)
    {
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return MasterLabels.None;
        var lines = view.Lines ?? [];
        return await MasterLabelResolver.ResolveAsync(inventory,
            lines.Select(l => l.Item?.ItemId),
            lines.Select(l => (l.Item?.SkuId, l.Item?.SkuLevel)),
            new[] { view.WarehouseId, view.StagingLocationId }
                .Concat(lines.SelectMany(l => l.Allocations ?? []).SelectMany(a => a.PlannedBins ?? []).Where(b => b.LocationCode is null).Select(b => b.LocationId))
                .Concat((view.PickTasks ?? []).Where(t => t.FromLocationCode is null).Select(t => t.FromLocationId)),
            ct);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        var error = reply.Error;
        _logger.LogWarning("Outbound {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, error?.Code, error?.CorrelationId ?? reply.CorrelationId);
        var details = error?.Code is { } code && DetailedCodes.Contains(code, StringComparer.Ordinal) ? error.Details : null;
        var message = _localizer[WarehouseErrorMessages.KeyForS2(error?.Code, reply.StatusCode)].Value;
        if (details is null) return WarehouseProxy.Error(reply.StatusCode, error?.Code, message, error?.CorrelationId ?? reply.CorrelationId);
        return new JsonResult(new
        {
            error = new { code = error!.Code, message, correlationId = error.CorrelationId ?? reply.CorrelationId, details },
            contractVersion = InventoryProxy.ContractVersion
        }) { StatusCode = reply.StatusCode };
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyForS2(code, status)].Value, correlationId);
}
