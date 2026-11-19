using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Services;

/// <summary>A refusal decided before anything is written (pack §21 S2 rule: no central row, no local event).</summary>
public sealed record OutboundRefusal(string Code, string Message, int Status, IReadOnlyDictionary<string, object?>? Details = null)
{
    public Response<T> As<T>() => Response<T>.Fail(Code, Message, Status, Details);
}

/// <summary>The outbound's warehouse and its resolved Staging node (§4.5 <c>StagingLocationId</c> rule).</summary>
public sealed record StagingChoice(Location Warehouse, Location Staging);

/// <summary>A line the plan could not cover (§8.17 steps 2–4) — the <c>details.lines[]</c> row of 422 <c>WMS_ALLOCATION_FAILED</c>.</summary>
public sealed record PlanFailure(int LineSeq, string Code, decimal Requested, decimal Allocatable);

/// <summary>The outcome of steps 1–4: a refusal, or the lines with their allocations (plus per-line failures and the preview rows).</summary>
public sealed class OutboundPlan
{
    public OutboundRefusal? Refusal { get; init; }

    public List<OutboundLine> Lines { get; } = [];

    public List<PlanFailure> Failures { get; } = [];

    public List<PreviewLineDto> Preview { get; } = [];

    public FefoOutboundMode Mode { get; set; }

    public static OutboundPlan Refused(OutboundRefusal refusal) => new() { Refusal = refusal };
}

/// <summary>
/// Pack §8.17 steps 1–4 (and §8.24 for Item lines) — READ ONLY: the warehouse and its Staging node, the line shapes, the level mix, the
/// PRODUCT-MASTER resolution (Item: the item's own SKU), the base unit, then the plan: FEFO order (<see cref="IWarehouseFefoReader"/>, the
/// <c>getFefoOrder</c> rule) limited by the FU03 scope free quantity and by the AVAILABLE stock on the PICKABLE nodes (the warehouse's
/// stockable nodes outside the Staging and Receiving subtrees — one definition for lotted, lotless and Item lines, r24); a lot with a free
/// quantity of 0 does not count in the order (Q-01 = A); greedy fill; the lot override with the company's FEFO outbound mode; the bin
/// distribution in location-code order with the open tasks' conservative remaining and this request's earlier distribution subtracted.
/// Used by the create command (then written) and by the preview (never written).
/// </summary>
public sealed class OutboundAllocationPlanner
{
    private readonly ILocationRepository _locations;
    private readonly IWarehouseLocationRoleRepository _roleRecords;
    private readonly LocationRoleReads _roles;
    private readonly IProductMasterClient _products;
    private readonly IWarehouseStockReader _stock;
    private readonly IWarehouseTaskRepository _tasks;
    private readonly IWarehouseFefoReader _fefo;
    private readonly IInventoryReservationPort _reservations;
    private readonly WarehouseSupport _support;

    public OutboundAllocationPlanner(ILocationRepository locations, IWarehouseLocationRoleRepository roleRecords, LocationRoleReads roles,
        IProductMasterClient products, IWarehouseStockReader stock, IWarehouseTaskRepository tasks, IWarehouseFefoReader fefo,
        IInventoryReservationPort reservations, WarehouseSupport support)
    {
        _locations = locations;
        _roleRecords = roleRecords;
        _roles = roles;
        _products = products;
        _stock = stock;
        _tasks = tasks;
        _fefo = fefo;
        _reservations = reservations;
        _support = support;
    }

    private static Dictionary<string, object?> D(params (string Key, object? Value)[] values)
        => values.Where(v => v.Value is not null).ToDictionary(v => v.Key, v => v.Value);

    private static string Q(decimal value) => InventoryQuantity.ToWire(value);

    // ── step 1: request shape (400, before the key lookup — pack §8.1 order) ─────────────────────────────────

    /// <summary>The 400 rules of the body; null = well formed. <paramref name="requireShipTo"/>: create (the preview may omit it).</summary>
    public static string? ShapeError(CreateOutboundShipmentInput input, bool requireShipTo)
    {
        if (input.Lines.Count is 0 or > OutboundAllocationRules.MaxLines) return $"lines must hold 1-{OutboundAllocationRules.MaxLines} lines.";
        if (input.OrderRef is { Length: > OutboundAllocationRules.MaxOrderRefLength }) return "orderRef must be at most 100 characters.";
        if (input.Note is { Length: > OutboundAllocationRules.MaxNoteLength }) return "note must be at most 1000 characters.";
        if (input.ShipTo is null)
        {
            if (requireShipTo) return "shipTo is required.";
        }
        else if (AddressError(input.ShipTo) is { } address)
        {
            return address;
        }

        var orderLines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in input.Lines)
        {
            var orderLine = line.OrderLineId?.Trim();
            if (string.IsNullOrEmpty(orderLine) || orderLine.Length > OutboundAllocationRules.MaxOrderLineIdLength)
                return "lines[].orderLineId is required (at most 100 characters).";
            if (!orderLines.Add(orderLine)) return "lines[].orderLineId must be unique within the outbound.";
            if (line.SkuLevel != SkuLevel.Item && line.SkuId is null) return "lines[].item.skuId is required unless skuLevel is Item.";
            if (string.IsNullOrWhiteSpace(line.UomId)) return "lines[].uomId is required.";
            if (!InventoryQuantity.TryParsePositive(line.Quantity, out _)) return "lines[].quantity must be a positive decimal with at most 6 fraction digits.";
            if (line.LotOverride is { } lot)
            {
                if (string.IsNullOrWhiteSpace(lot.LotId)) return "lines[].lotOverride.lotId is required.";
                // r24 (last look 02 F-5): over 1000 is the request format in EVERY mode — 400 INVALID_REQUEST, as INVENTORY's parser answers
                if (lot.FefoBypassReason is { } reason && reason.Trim().Length > OutboundAllocationRules.MaxReasonLength)
                    return "lines[].lotOverride.fefoBypassReason must be at most 1000 characters.";
            }
        }

        return null;
    }

    public static string? AddressError(OutboundAddressInput address)
    {
        if (string.IsNullOrWhiteSpace(address.Name) || string.IsNullOrWhiteSpace(address.Country)) return "shipTo.name and shipTo.country are required.";
        return new[] { address.Name, address.Country, address.City, address.PostalCode, address.Line1, address.Line2 }.Any(v => v is { Length: > 200 })
            ? "shipTo members must be at most 200 characters."
            : null;
    }

    // ── step 1: warehouse and Staging ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The warehouse (a LOCATION <c>Warehouse</c> of the company, Active — 422 UNKNOWN_LOCATION) and its Staging node: the stockable, Active nodes
    /// of the warehouse holding the Staging role, own or inherited (§4.4). None → 422 WMS_STAGING_NOT_CONFIGURED; one → the server picks it;
    /// several and none named → 400; a named node that is not one of them → 422 WMS_TARGET_NOT_ALLOWED (UNKNOWN_LOCATION when not a node of the
    /// company at all).
    /// </summary>
    public async Task<(StagingChoice? Choice, OutboundRefusal? Refusal)> StagingAsync(Guid warehouseId, Guid? stagingLocationId, CancellationToken ct)
    {
        var warehouse = await _locations.FindAsync(warehouseId, ct);
        if (warehouse is null || warehouse.Type != LocationType.Warehouse || warehouse.Status != LocationStatus.Active)
        {
            return (null, new OutboundRefusal(InventoryErrorCodes.UnknownLocation, "The warehouse does not exist in the selected company or is not active.", 422,
                D(("warehouseId", warehouseId.ToString("D")))));
        }

        var stagings = await StagingNodesAsync(warehouseId, ct);
        if (stagings.Count == 0)
        {
            return (null, new OutboundRefusal(WarehouseErrorCodes.StagingNotConfigured,
                "The warehouse has no Staging location; give a stockable location of it the Staging role first.", 422, D(("warehouseId", warehouseId.ToString("D")))));
        }

        if (stagingLocationId is not { } named)
        {
            return stagings.Count == 1
                ? (new StagingChoice(warehouse, stagings[0]), null)
                : (null, new OutboundRefusal(InventoryErrorCodes.InvalidRequest, "The warehouse has several Staging locations; stagingLocationId is required.", 400));
        }

        if (stagings.FirstOrDefault(s => s.Id == named) is { } chosen) return (new StagingChoice(warehouse, chosen), null);
        if (await _locations.FindAsync(named, ct) is null)
        {
            return (null, new OutboundRefusal(InventoryErrorCodes.UnknownLocation, "The Staging location does not exist in the selected company.", 422,
                D(("locationId", named.ToString("D")))));
        }

        return (null, new OutboundRefusal(WarehouseErrorCodes.TargetNotAllowed, "The location is not a Staging location of this warehouse.", 422,
            D(("rule", "NotStaging"), ("locationId", named.ToString("D")))));
    }

    /// <summary>The Staging nodes of the warehouse: the nodes of a Staging record and their subtree (inheritance), stockable and Active.</summary>
    private async Task<List<Location>> StagingNodesAsync(Guid warehouseId, CancellationToken ct)
    {
        var holders = await _roleRecords.FindWithRoleAsync(WarehouseRole.Staging, ct);
        if (holders.Count == 0) return [];
        var roots = (await _locations.FindManyAsync(holders.Select(h => h.LocationId).ToList(), ct))
            .Where(n => n.Id == warehouseId || n.AncestorIds.Contains(warehouseId)).ToList();
        var found = roots.ToDictionary(n => n.Id);
        var queue = new Queue<Guid>(roots.Select(r => r.Id));
        while (queue.Count > 0)
        {
            foreach (var child in await _locations.ListAsync(null, queue.Dequeue(), ct))
            {
                if (found.TryAdd(child.Id, child)) queue.Enqueue(child.Id);
            }
        }

        var candidates = found.Values.Where(n => n.IsStockable && n.Status == LocationStatus.Active && n.AncestorIds.Contains(warehouseId)).ToList();
        var views = await _roles.ViewsAsync(candidates.Select(c => c.Id).ToList(), ct);
        return candidates.Where(c => views.TryGetValue(c.Id, out var v) && v.EffectiveRoles.Contains(WarehouseRole.Staging))
            .OrderBy(c => c.Code, StringComparer.Ordinal).ToList();
    }

    // ── steps 1–4: lines and plan ───────────────────────────────────────────────────────────────────────────

    private sealed record ResolvedLine(int LineSeq, OutboundLineInput Input, string OrderLineId, Guid SkuId, decimal Quantity, string BaseUomId,
        bool LotControlled, bool SerialControlled, int? MinRemaining);

    private sealed record LotFigure(FefoEligibleLot Lot, decimal Allocatable, int? FreeRank);

    /// <summary>Per SKU of the request: the pickable rows, the open tasks' remaining per (bin, lot), the FEFO lots with their allocatable.</summary>
    private sealed class SkuWorld
    {
        public required List<WarehouseStockRow> Pickable { get; init; }

        public required Dictionary<(Guid Location, string? Lot), decimal> OpenRemaining { get; init; }

        public required Dictionary<Guid, string> Codes { get; init; }

        public required FefoOrder? Order { get; init; }

        public required List<LotFigure> Lots { get; init; }

        public decimal LotlessAllocatable { get; init; }

        /// <summary>What this request already took per lot (null = the lotless warehouse source).</summary>
        public Dictionary<string, decimal> Taken { get; } = new(StringComparer.Ordinal);

        /// <summary>What this request already planned per (bin, lot).</summary>
        public Dictionary<(Guid Location, string? Lot), decimal> Planned { get; } = [];
    }

    /// <summary>
    /// Steps 1 (lines) – 4. <paramref name="outboundId"/> names the reservation keys (the preview passes <see cref="Guid.Empty"/>).
    /// <paramref name="preview"/>: a lot override's FEFO deviation is reported, not refused.
    /// </summary>
    public async Task<OutboundPlan> PlanAsync(CreateOutboundShipmentInput input, Guid outboundId, StagingChoice staging, bool preview, CancellationToken ct)
    {
        // §8.24 rule 2: all Item or none
        if (input.Lines.Any(l => l.SkuLevel == SkuLevel.Item) && input.Lines.Any(l => l.SkuLevel != SkuLevel.Item))
        {
            return OutboundPlan.Refused(new OutboundRefusal(WarehouseErrorCodes.OutboundLevelMixed,
                "An outbound holds either Item lines only or no Item line; split the lines into two outbounds.", 422));
        }

        var resolved = new List<ResolvedLine>();
        try
        {
            var seq = 0;
            foreach (var line in input.Lines)
            {
                var (refusal, r) = await ResolveAsync(++seq, line, ct);
                if (refusal is not null) return OutboundPlan.Refused(refusal);
                resolved.Add(r!);
            }

            var plan = new OutboundPlan { Mode = await _fefo.OutboundModeAsync(ct) };
            var worlds = new Dictionary<(Guid Sku, SkuLevel Level), SkuWorld>();
            var allocSeq = 0;
            foreach (var line in resolved)
            {
                var key = (line.SkuId, line.Input.SkuLevel);
                if (!worlds.TryGetValue(key, out var world))
                {
                    world = await WorldAsync(staging.Warehouse.Id, line, ct);
                    worlds[key] = world;
                }

                var refusal = PlanLine(line, world, plan, outboundId, preview, ref allocSeq);
                if (refusal is not null) return OutboundPlan.Refused(refusal);
            }

            if (plan.Lines.Sum(l => l.Allocations.Count) > OutboundAllocationRules.MaxAllocations)
            {
                return OutboundPlan.Refused(new OutboundRefusal(WarehouseErrorCodes.AllocationTooManyLines,
                    $"The allocation needs more than {OutboundAllocationRules.MaxAllocations} lot / bin lines; split the outbound.", 422,
                    D(("allocations", plan.Lines.Sum(l => l.Allocations.Count)))));
            }

            return plan;
        }
        catch (ProductMasterUnavailableException)
        {
            return OutboundPlan.Refused(new OutboundRefusal(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; nothing was written.", 503));
        }
        catch (WarehouseInventoryUnavailableException)
        {
            return OutboundPlan.Refused(new OutboundRefusal(InventoryErrorCodes.InventoryUnavailable, "The stock could not be read; nothing was written.", 503));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is InvalidOperationException or TimeoutException)
        {
            // the FEFO candidate read includes the tenant-wide recall read; an unreadable source is 503, never "no stock"
            return OutboundPlan.Refused(new OutboundRefusal(InventoryErrorCodes.InventoryUnavailable, "The stock or FEFO order could not be read; nothing was written.", 503));
        }
    }

    /// <summary>
    /// PRODUCT-MASTER resolution of one line. Item (§8.24 rule 1): the item's own SKU — <c>skuId</c> absent → itemId, given → must equal it
    /// (D-SKU-LINK, <c>SkuIdMismatch</c>); <c>getSku(itemId, Item)</c> unknown → <c>UnknownItemSku</c>; <c>stockable: false</c> →
    /// <c>NotStockable</c>; a lifecycle that does not let stock move (<c>ProductMasterStockRules.Allows</c>, LOCATION_TRANSFER) →
    /// <c>LifecycleBlocked</c>; every one 422 <c>WMS_ITEM_NOT_RESOLVABLE</c>. Other levels: the SKU known at its level and linked to the item.
    /// Then the lot / serial behaviour and the base unit (<c>uomId</c> must be it — 422 <c>WMS_UOM_NOT_BASE</c>, no conversion).
    /// </summary>
    private async Task<(OutboundRefusal?, ResolvedLine?)> ResolveAsync(int lineSeq, OutboundLineInput line, CancellationToken ct)
    {
        _ = InventoryQuantity.TryParsePositive(line.Quantity, out var quantity);
        var item = line.ItemId;
        SkuReference? sku;
        Guid skuId;
        if (line.SkuLevel == SkuLevel.Item)
        {
            if (line.SkuId is { } given && given != item) return (ItemRefusal(lineSeq, line, "SkuIdMismatch"), null);
            skuId = item;
            sku = await _products.GetSkuAsync(skuId, SkuLevel.Item, ct);
            if (sku is null || (Guid.TryParse(sku.ItemId, out var linked) && linked != item)) return (ItemRefusal(lineSeq, line, "UnknownItemSku"), null);
            if (!ProductMasterStockRules.IsStockable(sku.Stockable)) return (ItemRefusal(lineSeq, line, "NotStockable"), null);
            if (!ProductMasterStockRules.Allows(ProductMasterStockRules.Lifecycle(sku.LifecycleStatus), MovementType.LocationTransfer))
                return (ItemRefusal(lineSeq, line, "LifecycleBlocked"), null);
        }
        else
        {
            skuId = line.SkuId!.Value;
            sku = await _products.GetSkuAsync(skuId, line.SkuLevel, ct);
            if (sku is null)
            {
                return (new OutboundRefusal(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not know this SKU at this level.", 422,
                    D(("lineSeq", lineSeq), ("skuId", skuId.ToString("D")), ("skuLevel", WireNames.ToWire(line.SkuLevel)))), null);
            }

            if (Guid.TryParse(sku.ItemId, out var linked) && linked != item)
            {
                return (new OutboundRefusal(InventoryErrorCodes.SkuItemMismatch, "The SKU does not belong to the item.", 422,
                    D(("lineSeq", lineSeq), ("skuId", skuId.ToString("D")), ("itemId", item.ToString("D")))), null);
            }

            if (!ProductMasterStockRules.IsStockable(sku.Stockable))
            {
                return (new OutboundRefusal(InventoryErrorCodes.SkuLevelNotStockable, "The SKU holds no stock (stockable: false).", 422,
                    D(("lineSeq", lineSeq), ("skuId", skuId.ToString("D")))), null);
            }
        }

        var productSerial = line.SkuLevel == SkuLevel.Gsku ? (await _products.GetProductAsync(item, ct))?.SerialControlled : null;
        var behaviour = ProductMasterStockRules.Behaviour(line.SkuLevel, sku.LotControlled, line.SkuLevel == SkuLevel.Item ? false : sku.SerialControlled, productSerial);
        if (behaviour.Refusal is { } refused)
        {
            return refused == StockBehaviourRefusal.LevelNotStockable
                ? (new OutboundRefusal(InventoryErrorCodes.SkuLevelNotStockable,
                    "A GSKU-level line needs a product whose serial flag is explicitly false; serial-tracked stock is held at Lsku / FinishedGood.", 422,
                    D(("lineSeq", lineSeq))), null)
                : (new OutboundRefusal(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not state the lot / serial flag this SKU level needs.", 422,
                    D(("lineSeq", lineSeq))), null);
        }

        var uom = await _products.GetSkuUomAsync(skuId, ct);
        if (uom is null)
        {
            return (new OutboundRefusal(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not state the SKU's base unit.", 422,
                D(("lineSeq", lineSeq))), null);
        }

        if (!string.Equals(line.UomId!.Trim(), uom.BaseUomId, StringComparison.Ordinal))
        {
            return (new OutboundRefusal(WarehouseErrorCodes.UomNotBase, "The line's unit must be the SKU's base unit (no conversion).", 422,
                D(("lineSeq", lineSeq), ("uomId", line.UomId), ("baseUomId", uom.BaseUomId))), null);
        }

        return (null, new ResolvedLine(lineSeq, line, line.OrderLineId!.Trim(), skuId, quantity, uom.BaseUomId, behaviour.LotControlled, behaviour.SerialControlled,
            sku.MinRemainingShelfLifeDays));
    }

    private static OutboundRefusal ItemRefusal(int lineSeq, OutboundLineInput line, string reason)
        => new(WarehouseErrorCodes.ItemNotResolvable, $"The Item line cannot be resolved to the item's own SKU ({reason}).", 422,
            D(("reason", reason), ("lineSeq", lineSeq), ("itemId", line.ItemId.ToString("D"))));

    /// <summary>
    /// The SKU's stock picture in the warehouse: the AVAILABLE rows on PICKABLE nodes (Active, stockable, under the warehouse, neither Staging
    /// nor Receiving — own or inherited, r23 / r24), the open tasks' remaining on them, and the FEFO lots with their allocatable quantity
    /// = min(FEFO available, FU03 scope free, pickable AVAILABLE) — the free rank counts only lots whose allocatable is &gt; 0 (Q-01 = A).
    /// </summary>
    private async Task<SkuWorld> WorldAsync(Guid warehouseId, ResolvedLine line, CancellationToken ct)
    {
        var level = line.Input.SkuLevel;
        var rows = await _stock.AvailableRowsOfSkuAsync(warehouseId, line.Input.ItemId, line.SkuId, level, ct);
        var views = await _roles.ViewsAsync(rows.Select(r => r.LocationId).Distinct().ToList(), ct);
        var pickable = rows.Where(r => views.TryGetValue(r.LocationId, out var v) && Pickable(v, warehouseId)).ToList();
        var codes = views.ToDictionary(v => v.Key, v => v.Value.Location.Code);

        var open = (await _tasks.OpenTasksFromAsync(pickable.Select(r => r.LocationId).Distinct().ToList(), ct))
            .Where(t => t.WarehouseId == warehouseId && t.SkuId == line.SkuId && t.SkuLevel == level)
            .GroupBy(t => (t.FromLocationId, t.LotId))
            .ToDictionary(g => (g.Key.FromLocationId, g.Key.LotId), g => g.Sum(OutboundAllocationRules.OpenTaskRemaining));

        if (!line.LotControlled)
        {
            var free = await _reservations.GetScopeFreeAsync(line.Input.ItemId, line.SkuId, level, warehouseId.ToString("D"), null, ct);
            var onPickable = pickable.Where(r => r.LotId is null).Sum(r => r.OnHand);
            return new SkuWorld
            {
                Pickable = pickable, OpenRemaining = open, Codes = codes, Order = null, Lots = [],
                LotlessAllocatable = Math.Max(0m, Math.Min(free, onPickable))
            };
        }

        var order = await _fefo.OrderAsync(warehouseId, line.Input.ItemId, line.SkuId, level, line.MinRemaining, _support.Now(), ct);
        var figures = new List<LotFigure>();
        var rank = 0;
        foreach (var lot in order.Eligible)
        {
            var lotId = lot.Lot.Id.ToString("D");
            var free = await _reservations.GetScopeFreeAsync(line.Input.ItemId, line.SkuId, level, warehouseId.ToString("D"), lot.Lot.Id, ct);
            var onPickable = pickable.Where(r => r.LotId == lotId).Sum(r => r.OnHand);
            var allocatable = Math.Max(0m, Math.Min(lot.Available, Math.Min(free, onPickable)));
            figures.Add(new LotFigure(lot, allocatable, allocatable > 0m ? ++rank : null));
        }

        return new SkuWorld { Pickable = pickable, OpenRemaining = open, Codes = codes, Order = order, Lots = figures };
    }

    private static bool Pickable(LocationRoleView view, Guid warehouseId)
        => view.Location.Status == LocationStatus.Active && view.Location.IsStockable && view.Location.AncestorIds.Contains(warehouseId)
           && !view.EffectiveRoles.Contains(WarehouseRole.Staging) && !view.EffectiveRoles.Contains(WarehouseRole.Receiving);

    /// <summary>Steps 2–4 for one line; a refusal (FEFO mode, lot override, serial) stops the whole request, a short line is a failure row.</summary>
    private OutboundRefusal? PlanLine(ResolvedLine line, SkuWorld world, OutboundPlan plan, Guid outboundId, bool preview, ref int allocSeq)
    {
        var free = world.Lots.Where(l => l.FreeRank is not null).ToList();
        var sources = new List<AllocationSource>();
        string? bypassReason = null;
        PreviewDeviationDto? deviation = null;

        if (line.Input.LotOverride is { } chosen)
        {
            var lotId = chosen.LotId!.Trim();
            var figure = line.LotControlled ? world.Lots.FirstOrDefault(l => l.Lot.Lot.Id.ToString("D") == lotId) : null;
            if (figure is null)
            {
                var excluded = world.Order?.Excluded.FirstOrDefault(x => x.Lot.Id.ToString("D") == lotId);
                var code = excluded?.Reason switch
                {
                    FefoExclusionReason.Recalled => InventoryErrorCodes.LotUnderRecall,
                    FefoExclusionReason.Expired => InventoryErrorCodes.LotExpired,
                    FefoExclusionReason.Blocked => InventoryErrorCodes.LotBlocked,
                    FefoExclusionReason.RetestDue => InventoryErrorCodes.LotRetestDue,
                    _ => WarehouseErrorCodes.LotNotAllocatable
                };
                return new OutboundRefusal(code, code == WarehouseErrorCodes.LotNotAllocatable
                        ? "The chosen lot is not in the FEFO list of eligible lots for this SKU and warehouse."
                        : "The chosen lot cannot leave the warehouse.", 422,
                    D(("lineSeq", line.LineSeq), ("lotId", lotId), ("lotNumber", excluded?.Lot.LotNumber),
                        ("movementType", code == InventoryErrorCodes.LotUnderRecall ? "LOCATION_TRANSFER" : null)));
            }

            // §8.17 step 3: a deviation when the chosen lot is not the first FREE lot (the hook's rule: an earlier expiry among the free lots)
            var decision = FefoIssueRule.Evaluate(figure.Lot.Lot, new FefoOrder(free.Select(f => f.Lot).ToList(), []));
            if (!decision.Compliant)
            {
                var first = decision.FirstEligible!;
                deviation = new PreviewDeviationDto(first.Id.ToString("D"), first.LotNumber, Day(first.ExpiryDate), plan.Mode.ToString());
                var reason = OutboundAllocationRules.Reason(chosen.FefoBypassReason);
                if (!preview)
                {
                    var details = D(("lineSeq", line.LineSeq), ("firstEligibleLotId", first.Id.ToString("D")), ("firstEligibleLotNumber", first.LotNumber),
                        ("firstEligibleExpiry", Day(first.ExpiryDate)), ("lotId", lotId));
                    if (plan.Mode == FefoOutboundMode.Block)
                    {
                        return new OutboundRefusal(InventoryErrorCodes.FefoOrderViolation,
                            $"FEFO: lot {first.LotNumber} must be issued first; this company refuses a lot out of FEFO order.", 422, details);
                    }

                    if (plan.Mode == FefoOutboundMode.RequireReason && reason is not { Length: >= OutboundAllocationRules.MinReasonLength })
                    {
                        return new OutboundRefusal(InventoryErrorCodes.FefoBypassReasonRequired,
                            $"FEFO: lot {first.LotNumber} expires earlier; choosing another lot needs fefoBypassReason (10-1000 characters).", 422, details);
                    }
                }

                bypassReason = reason;
            }

            sources.Add(new AllocationSource(lotId, figure.Lot.Lot.LotNumber, figure.FreeRank, Left(world, lotId, figure.Allocatable)));
        }
        else if (line.LotControlled)
        {
            sources.AddRange(free.Select(f => new AllocationSource(f.Lot.Lot.Id.ToString("D"), f.Lot.Lot.LotNumber, f.FreeRank,
                Left(world, f.Lot.Lot.Id.ToString("D"), f.Allocatable))));
        }
        else
        {
            sources.Add(new AllocationSource(null, null, null, Left(world, null, world.LotlessAllocatable)));
        }

        var allocatable = sources.Sum(s => s.Allocatable);
        var taken = OutboundAllocationRules.Fill(line.Quantity, sources);
        var outboundLine = new OutboundLine
        {
            LineSeq = line.LineSeq, OrderLineId = line.OrderLineId, ItemId = line.Input.ItemId, SkuId = line.SkuId, SkuLevel = line.Input.SkuLevel,
            RequestedQuantity = line.Quantity, BaseUomId = line.BaseUomId, SerialControlled = line.SerialControlled,
            LotOverride = line.Input.LotOverride is { } o ? new OutboundLotOverride { LotId = o.LotId!.Trim(), FefoBypassReason = bypassReason } : null
        };

        string? failure = null;
        var allocated = taken.Sum(t => t.Quantity);
        if (allocated < line.Quantity)
        {
            failure = "InsufficientQuantity";
        }
        else
        {
            foreach (var (source, quantity) in taken)
            {
                var bins = world.Pickable.Where(r => r.LotId == source.LotId)
                    .GroupBy(r => r.LocationId)
                    .Select(g =>
                    {
                        var onHand = g.Sum(r => r.OnHand);
                        var usable = onHand - world.OpenRemaining.GetValueOrDefault((g.Key, source.LotId)) - world.Planned.GetValueOrDefault((g.Key, source.LotId));
                        return new PlanBin(g.Key, world.Codes.GetValueOrDefault(g.Key) ?? g.Key.ToString("D"), usable, onHand);
                    }).ToList();
                var distribution = OutboundAllocationRules.Distribute(quantity, bins, line.SerialControlled, out var serialPartial);
                if (serialPartial && !preview)
                {
                    return new OutboundRefusal(WarehouseErrorCodes.SerialPartialNotSupported,
                        "A serial-controlled SKU is picked whole: the allocation would take a serial bucket partly.", 422, D(("lineSeq", line.LineSeq)));
                }

                if (distribution is null)
                {
                    failure = "BinCapacityShort";
                    break;
                }

                var allocation = new OutboundAllocation
                {
                    AllocSeq = ++allocSeq, LotId = source.LotId, LotNumber = source.LotNumber, Quantity = quantity, FefoRank = source.FefoRank,
                    FefoBypassReason = line.Input.LotOverride is not null ? bypassReason : null,
                    PlannedBins = distribution.Select(d => new OutboundPlannedBin { LocationId = d.Bin.LocationId, Quantity = d.Quantity }).ToList(),
                    ReservationKey = OutboundAllocationRules.ReservationKey(outboundId, allocSeq)
                };
                outboundLine.Allocations.Add(allocation);
                Take(world, source.LotId, quantity);
                foreach (var (bin, q) in distribution) world.Planned[(bin.LocationId, source.LotId)] = world.Planned.GetValueOrDefault((bin.LocationId, source.LotId)) + q;
            }
        }

        if (failure is not null) plan.Failures.Add(new PlanFailure(line.LineSeq, failure, line.Quantity, allocatable));
        plan.Lines.Add(outboundLine);
        plan.Preview.Add(new PreviewLineDto(line.LineSeq, line.OrderLineId,
            new OutboundItemDto(line.Input.ItemId.ToString("D"), line.SkuId.ToString("D"), WireNames.ToWire(line.Input.SkuLevel)),
            Q(line.Quantity), Q(outboundLine.Allocations.Sum(a => a.Quantity)), failure, Q(allocatable),
            outboundLine.Allocations.Select(a => new PreviewAllocationDto(a.LotId, a.LotNumber, a.FefoRank, Q(a.Quantity),
                a.PlannedBins.Select(b => new OutboundPlannedBinDto(b.LocationId.ToString("D"), world.Codes.GetValueOrDefault(b.LocationId), Q(b.Quantity))).ToList())).ToList(),
            free.Select(f => new PreviewLotDto(f.Lot.Lot.Id.ToString("D"), f.Lot.Lot.LotNumber, Day(f.Lot.Lot.ExpiryDate), f.FreeRank!.Value, Q(f.Allocatable))).ToList(),
            deviation));
        return null;
    }

    private static decimal Left(SkuWorld world, string? lotId, decimal allocatable)
        => Math.Max(0m, allocatable - world.Taken.GetValueOrDefault(lotId ?? string.Empty));

    private static void Take(SkuWorld world, string? lotId, decimal quantity)
        => world.Taken[lotId ?? string.Empty] = world.Taken.GetValueOrDefault(lotId ?? string.Empty) + quantity;

    private static string? Day(DateTimeOffset? value)
        => value is { } v ? DateOnly.FromDateTime(v.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
}
