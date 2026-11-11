using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Entities;

// MOD-0178 S2 outbound shipment (pack §4.5, r20). An outbound keeps NO stock: its quantities are the requested / allocated / planned
// quantities of the shipment, the stock itself is the MOD-0173 ledger's and the hold is the MOD-0173-FU03 reservation's (pack §2 boundary
// rule 1). Every write is a compare-and-set on EntityBase.Version.

/// <summary>WAREHOUSE-OUTBOUND 1.0.0 <c>Address</c> (frozen required members <c>name</c>, <c>country</c>). KVKK: never in a log, an OTel
/// attribute or a local event's <c>Data</c> — only a "changed" mark (pack §21).</summary>
public sealed class OutboundAddress
{
    public string Name { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? Line1 { get; set; }

    public string? Line2 { get; set; }

    public OutboundAddress Copy() => (OutboundAddress)MemberwiseClone();

    public bool SameAs(OutboundAddress? other)
        => other is not null && Name == other.Name && Country == other.Country && City == other.City && PostalCode == other.PostalCode
           && Line1 == other.Line1 && Line2 == other.Line2;
}

/// <summary>§4.5 <c>LotOverride</c> — the lot the line must come from (§8.17 step 3) and the FEFO bypass reason the request sent.</summary>
public sealed class OutboundLotOverride
{
    public string LotId { get; set; } = string.Empty;

    public string? FefoBypassReason { get; set; }
}

/// <summary>§4.5 <c>PlannedBins[]</c> — the persisted pick plan of one allocation (r23): which bin gives how much.</summary>
public sealed class OutboundPlannedBin
{
    public Guid LocationId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>
/// §4.5 <c>OutboundAllocation</c> — one lot (or, for a SKU without lot tracking, the warehouse) of one line, with its FU03 reservation.
/// The plan (lot, quantity, rank, reason, key, bins) is written in transaction A and never recomputed (§8.17 step 5: recovery sends the
/// SAME batch); <see cref="ReservationId"/> is set by transaction B.
/// </summary>
public sealed class OutboundAllocation
{
    public int AllocSeq { get; set; }

    public string? LotId { get; set; }

    public string? LotNumber { get; set; }

    public decimal Quantity { get; set; }

    public int? FefoRank { get; set; }

    public string? FefoBypassReason { get; set; }

    public List<OutboundPlannedBin> PlannedBins { get; set; } = [];

    public Guid? ReservationId { get; set; }

    /// <summary><c>wms:out:{outboundId}:{allocSeq}</c> — the FU03 reservation idempotency key; deterministic, the same on every attempt.</summary>
    public string ReservationKey { get; set; } = string.Empty;

    public ReservationReleaseState ReservationReleaseState { get; set; } = ReservationReleaseState.None;

    public OutboundAllocation Copy()
    {
        var copy = (OutboundAllocation)MemberwiseClone();
        copy.PlannedBins = PlannedBins.Select(b => new OutboundPlannedBin { LocationId = b.LocationId, Quantity = b.Quantity }).ToList();
        return copy;
    }
}

/// <summary>§4.5 <c>OutboundLine</c> (embedded).</summary>
public sealed class OutboundLine
{
    public int LineSeq { get; set; }

    public string OrderLineId { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public decimal RequestedQuantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public bool SerialControlled { get; set; }

    public OutboundLotOverride? LotOverride { get; set; }

    public List<OutboundAllocation> Allocations { get; set; } = [];

    public OutboundLine Copy()
    {
        var copy = (OutboundLine)MemberwiseClone();
        copy.LotOverride = LotOverride is null ? null : new OutboundLotOverride { LotId = LotOverride.LotId, FefoBypassReason = LotOverride.FefoBypassReason };
        copy.Allocations = Allocations.Select(a => a.Copy()).ToList();
        return copy;
    }
}

/// <summary>One FU03 line result of a refused batch (§4.5 <c>AllocationFailure.lines[]</c>).</summary>
public sealed class OutboundAllocationFailureLine
{
    public int LineSeq { get; set; }

    public string? Code { get; set; }

    public decimal Requested { get; set; }

    public decimal? Allocatable { get; set; }

    public OutboundAllocationFailureLine Copy() => (OutboundAllocationFailureLine)MemberwiseClone();
}

/// <summary>
/// §4.5 <c>AllocationFailure</c> (r24) — written ONCE by the write that makes the outbound <see cref="OutboundStatus.AllocationFailed"/>; the
/// source of the 422 replay and of the S3 <c>AllocationOrphanSuspected</c> exception (<see cref="AllocationFailureKind.KeyReused"/>).
/// </summary>
public sealed class OutboundAllocationFailure
{
    public AllocationFailureKind Kind { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public List<OutboundAllocationFailureLine> Lines { get; set; } = [];

    public OutboundAllocationFailure Copy()
    {
        var copy = (OutboundAllocationFailure)MemberwiseClone();
        copy.Lines = Lines.Select(l => (OutboundAllocationFailureLine)l.Copy()).ToList();
        return copy;
    }
}

/// <summary>
/// §4.5 <c>Shipment</c> (DL-5, §8.21) — the MVP-6 shipment the dispatch sync saw; <see cref="ObservedAtUtc"/> is the moment of the sync (the
/// MVP-6 wire carries no dispatch time). Shown on <c>/view</c> only, never on the frozen detail (<c>SOURCE_DRIFT</c>, Q-M6-1).
/// </summary>
public sealed class OutboundShipmentLink
{
    public string ShipmentId { get; set; } = string.Empty;

    public string? ShipmentNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; set; }

    public OutboundShipmentLink Copy() => (OutboundShipmentLink)MemberwiseClone();
}

/// <summary>
/// §4.5 <c>ShortDispatchUnresolved[]</c> (r24 / r25, §8.21) — one row per SKU whose unissued picked quantity could not be put back by a task,
/// written with <c>Shipped</c> and never changed; <see cref="LotId"/> only when every line of the SKU was on one lot.
/// </summary>
public sealed class OutboundShortDispatchUnresolved
{
    public Guid SkuId { get; set; }

    public string? LotId { get; set; }

    public decimal Expected { get; set; }

    public decimal Issued { get; set; }

    /// <summary><c>LotMissing</c> → <c>NotFromStaging</c> → <c>OverIssued</c> — the first condition that did not hold (explanatory only).</summary>
    public string Reason { get; set; } = string.Empty;

    public OutboundShortDispatchUnresolved Copy() => (OutboundShortDispatchUnresolved)MemberwiseClone();
}

/// <summary>§4.5 <c>OutboundShipment</c> (<c>wms_outbound_shipments</c>; the source of WAREHOUSE-OUTBOUND <c>OutboundShipment</c>).</summary>
public sealed class OutboundShipment : EntityBase
{
    /// <summary>RCS-001 <c>{COMPANY}-OUT-{000001}</c> from the temporary counter (Q-07 B); written once, never changes.</summary>
    public string OutboundNumber { get; set; } = string.Empty;

    public string CompanyCodePrefix { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public string? WarehouseCode { get; set; }

    public Guid StagingLocationId { get; set; }

    public string? StagingLocationCode { get; set; }

    public string? OrderRef { get; set; }

    public OutboundAddress ShipTo { get; set; } = new();

    public string? Note { get; set; }

    public OutboundStatus Status { get; set; } = OutboundStatus.Allocating;

    public List<OutboundLine> Lines { get; set; } = [];

    public DateTimeOffset? ReadyAtUtc { get; set; }

    /// <summary>Only while <see cref="OutboundStatus.Allocating"/>: the recovery lock (§8.17 step 6) — the maintenance service waits until it passed.</summary>
    public DateTimeOffset? AllocationLeaseUntilUtc { get; set; }

    public OutboundAllocationFailure? AllocationFailure { get; set; }

    /// <summary>DL-5: the MVP-6 shipment that made the outbound <c>Shipped</c> (§8.21).</summary>
    public OutboundShipmentLink? Shipment { get; set; }

    /// <summary>DL-5: the cancel reason (10–1000, §8.22).</summary>
    public string? CancelReason { get; set; }

    /// <summary>DL-5: written with <c>Shipped</c> only (§8.21), never changed afterwards.</summary>
    public List<OutboundShortDispatchUnresolved> ShortDispatchUnresolved { get; set; } = [];

    /// <summary>
    /// DL-5: raised by every pick task write that makes a task terminal, in the SAME transaction — two concurrent last confirmations of two
    /// tasks then both write this document, so one of them conflicts and re-reads (no write skew: the outbound becomes <c>Picked</c> once,
    /// pack §8.19 "eşzamanlı iki son onay"). Not a quantity and not the version (a header edit's If-Match is not touched).
    /// </summary>
    public int PickProgress { get; set; }

    public Guid CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? UpdatedByName { get; set; }

    /// <summary>The create key (unique per company + <see cref="CreatedBy"/>) and its request fingerprint (§8.1).</summary>
    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public Guid CorrelationId { get; set; }

    /// <summary>The last timeline <c>Seq</c> written for this outbound (each timeline row of a write takes the next one, in the same CAS write).</summary>
    public int EventSeq { get; set; }

    /// <summary>Every allocation of every line, in line then allocation order.</summary>
    public IEnumerable<OutboundAllocation> AllAllocations() => Lines.SelectMany(l => l.Allocations);

    /// <summary>W-6 (§8.24): an Item-level outbound — every line is Item (a mix is refused at creation).</summary>
    public bool IsItemLevel() => Lines.Count > 0 && Lines.All(l => l.SkuLevel == SkuLevel.Item);

    public OutboundShipment Copy()
    {
        var copy = (OutboundShipment)MemberwiseClone();
        copy.ShipTo = ShipTo.Copy();
        copy.Lines = Lines.Select(l => l.Copy()).ToList();
        copy.AllocationFailure = AllocationFailure?.Copy();
        copy.Shipment = Shipment?.Copy();
        copy.ShortDispatchUnresolved = ShortDispatchUnresolved.Select(u => u.Copy()).ToList();
        return copy;
    }
}

/// <summary>
/// §4.5 — the insert-only outbound timeline (<c>wms_outbound_events</c>), written in the SAME transaction as the write it describes. The key
/// event of a non-create command carries its request key, operation and fingerprint (§4.3 rule). <see cref="Data"/> never holds a
/// <c>ShipTo</c> value — only <c>shipToChanged</c> / <c>shipToEntered</c>.
/// </summary>
public sealed class OutboundShipmentEvent : EntityBase
{
    public Guid OutboundId { get; set; }

    public int Seq { get; set; }

    public OutboundEventKind Kind { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorName { get; set; }

    public bool System { get; set; }

    public DateTimeOffset AtUtc { get; set; }

    public Dictionary<string, string?> Data { get; set; } = [];

    public Guid CorrelationId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestOp { get; set; }

    public string? RequestFingerprint { get; set; }
}
