using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Services;

/// <summary>
/// DL-5 — the frozen WAREHOUSE-OUTBOUND 1.0.0 bodies (pack §8.21, AC-30) and the S2 put-back tasks (§8.21 / §8.22).
/// <para><b>Frozen body:</b> exactly the 1.0.0 members + the WMS draft §2.3 additive members (its single list; <c>Package.contents</c> and
/// <c>SkuLevel: Item</c> are "next contract version" there and are NOT written). No member is ever a JSON null (an absent value is not
/// written — GAP-0183-03), ids are canonical UUIDs, one line per allocation with a picked quantity &gt; 0 (an allocation closed short with 0 is
/// never written — r21 F-8), <c>quantity</c> = the picked quantity, <c>packageId</c> = the package NUMBER, only CLOSED packages. The
/// <c>shipment</c> member is never written here (only on <c>/view</c> — SOURCE_DRIFT, Q-M6-1). Deterministic: the same record gives the same
/// bytes (the MOD-0183 §8.2 drift hash), and after <c>ReadyToShip</c> only <c>status</c> can change.</para>
/// </summary>
public static class OutboundFlowReads
{
    public static Dictionary<string, object?> Frozen(OutboundShipment o, IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<HandlingUnit> packages)
    {
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var lines = new List<Dictionary<string, object?>>();
        foreach (var line in o.Lines)
        {
            foreach (var a in line.Allocations)
            {
                var quantity = picked.GetValueOrDefault(a.AllocSeq);
                if (quantity <= 0m) continue;
                var row = new Dictionary<string, object?>
                {
                    ["orderLineId"] = line.OrderLineId,
                    ["itemId"] = line.ItemId.ToString("D"),
                    ["skuId"] = line.SkuId.ToString("D"),
                    ["skuLevel"] = WireNames.ToWire(line.SkuLevel)
                };
                Put(row, "lotNumber", a.LotNumber);
                row["quantity"] = InventoryQuantity.ToWire(quantity);
                row["uomId"] = line.BaseUomId;
                // WMS draft §2.3 (WMS-BUNDLE 1.1.0, written from the first ReadyToShip on)
                Put(row, "lotId", a.LotId);
                row["fromWarehouseId"] = o.WarehouseId.ToString("D");
                row["fromLocationId"] = o.StagingLocationId.ToString("D");
                row["stockStatus"] = WireNames.ToWire(StockStatus.Available);
                Put(row, "reservationId", a.ReservationId?.ToString("D"));
                Put(row, "fefoBypassReason", a.FefoBypassReason);
                lines.Add(row);
            }
        }

        var body = new Dictionary<string, object?>
        {
            ["outboundId"] = o.Id.ToString("D"),
            ["outboundNumber"] = o.OutboundNumber,
            ["legalEntityId"] = (o.LegalEntityId ?? Guid.Empty).ToString("D"),
            ["warehouseId"] = o.WarehouseId.ToString("D")
        };
        Put(body, "orderRef", o.OrderRef);
        body["status"] = o.Status.ToString();
        var shipTo = new Dictionary<string, object?> { ["name"] = o.ShipTo.Name, ["country"] = o.ShipTo.Country };
        Put(shipTo, "city", o.ShipTo.City);
        Put(shipTo, "postalCode", o.ShipTo.PostalCode);
        Put(shipTo, "line1", o.ShipTo.Line1);
        Put(shipTo, "line2", o.ShipTo.Line2);
        body["shipTo"] = shipTo;
        body["lines"] = lines;
        body["packages"] = packages.Where(p => p.Status == HandlingUnitStatus.Closed).OrderBy(p => p.PackageNumber, StringComparer.Ordinal).Select(p =>
        {
            var row = new Dictionary<string, object?> { ["packageId"] = p.PackageNumber };
            if (p.WeightKg is { } w) row["weightKg"] = w.ToString("0.000", CultureInfo.InvariantCulture);
            Put(row, "dimensionsCm", p.DimensionsCm);
            return row;
        }).ToList();
        if (o.ReadyAtUtc is { } ready) body["readyAt"] = OutboundShipmentReads.Wire(ready);
        body["contractVersion"] = WarehouseContract.ContractVersion;
        return body;
    }

    /// <summary>The <c>OutboundShipmentReady</c> event body (WAREHOUSE-OUTBOUND <c>OutboundShipmentReadyEvent</c> + §2.3 / Q-M6-5 members).</summary>
    public static Dictionary<string, object?> ReadyEvent(OutboundShipment o, Guid eventId, Guid correlationId)
    {
        var body = new Dictionary<string, object?>
        {
            ["eventId"] = eventId.ToString("D"),
            ["outboundId"] = o.Id.ToString("D"),
            ["outboundNumber"] = o.OutboundNumber,
            ["tenantId"] = (o.TenantId ?? Guid.Empty).ToString("D"),
            ["legalEntityId"] = (o.LegalEntityId ?? Guid.Empty).ToString("D"),
            ["warehouseId"] = o.WarehouseId.ToString("D")
        };
        Put(body, "orderRef", o.OrderRef);
        body["readyAt"] = OutboundShipmentReads.Wire(o.ReadyAtUtc!.Value);
        body["correlationId"] = correlationId.ToString("D");
        return body;
    }

    private static void Put(Dictionary<string, object?> row, string name, string? value)
    {
        if (value is not null) row[name] = value;
    }

    /// <summary>
    /// A put-back putaway task from the outbound's Staging node (§8.21 <c>ShortDispatch</c> / §8.22 <c>Cancellation</c> — the two origins whose
    /// source may hold the Staging role, §12): one per (SKU, lot) bucket, planned = the residue, lot / SKU from the allocation, no target yet.
    /// </summary>
    public static WarehouseTask Putback(OutboundShipment o, WarehouseTaskOrigin origin, ShortDispatchPutback p, string number, WarehouseSupport support,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            Number = number,
            CompanyCodePrefix = o.CompanyCodePrefix,
            WarehouseCode = o.WarehouseCode,
            Kind = WarehouseTaskKind.Putaway,
            Status = WarehouseTaskStatus.Open,
            Origin = origin,
            SourceRef = new WarehouseTaskSourceRef
            {
                SourceModule = WarehouseContract.ModuleId, SourceType = OutboundAllocationRules.ReservationType, SourceDocumentId = o.Id.ToString("D")
            },
            WarehouseId = o.WarehouseId,
            ItemId = p.ItemId,
            SkuId = p.SkuId,
            SkuLevel = p.Level,
            LotId = p.LotId,
            LotNumber = p.LotNumber,
            SerialControlled = p.SerialControlled,
            StockStatus = StockStatus.Available,
            FromLocationId = o.StagingLocationId,
            PlannedQuantity = p.Quantity,
            BaseUomId = p.BaseUomId,
            CreatedBy = support.UserId,
            CreatedByName = support.ActorName,
            CorrelationId = support.CorrelationId,
            OpenSerialSlot = p.SerialControlled ? true : null,
            OutboundNumber = o.OutboundNumber
        };

    public static WarehouseTaskEvent PutbackCreated(WarehouseTask task, OutboundShipment o, WarehouseSupport support, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        TaskId = task.Id,
        Seq = ++task.EventSeq,
        Kind = WarehouseTaskEventKind.Created,
        ActorUserId = support.UserId,
        ActorName = support.ActorName,
        AtUtc = now,
        CorrelationId = support.CorrelationId,
        Data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["number"] = task.Number, ["origin"] = task.Origin.ToString(), ["outboundId"] = o.Id.ToString("D"), ["outboundNumber"] = o.OutboundNumber,
            ["fromLocationId"] = task.FromLocationId.ToString("D"), ["lotId"] = task.LotId, ["plannedQuantity"] = InventoryQuantity.ToWire(task.PlannedQuantity)
        }
    };
}
