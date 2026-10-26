using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Recall.Trace;

// MOD-0177 trace (pack §8.2). The engine (Application) walks the in-process reads; these are its pure building blocks and its result.

/// <summary>Pack §8.2 node kinds. Their order is the kind order of the determinism rule (§8.2 rule 10).</summary>
public enum TraceNodeKind
{
    RawMaterialLot,
    FinishedGoodLot,
    Lot,
    Input,
    Stock,
    Shipment,
    Disposal,
    Gap
}

/// <summary>Pack §8.2 rule 8 — the trace gaps (each a red row; "Takip dışı" is NOT one, KR-D).</summary>
public enum TraceGapCode
{
    LOTLESS_OUTBOUND,
    UNRESOLVED_TYPED_LOT,
    MISSING_GENEALOGY_LINK,
    CONSUMED_WITHOUT_OUTPUT_LINK,
    NO_CUSTOMER_REFERENCE,
    LIMIT_REACHED,
    OTHER_COMPANY_ACTIVITY
}

/// <summary>Pack §8.6 — what the shipment read gave. Only <see cref="Ok"/> adds MVP-6 fields; the trace continues in every case.</summary>
public enum ShipmentReadOutcome
{
    NotApplicable,
    Ok,
    Forbidden,
    NotFound,
    Unavailable
}

/// <summary>Where a node sits: the root, the backward closure (inputs) or the forward closure (what was made / shipped).</summary>
public enum TraceSide
{
    Root,
    Backward,
    Forward
}

/// <summary>The ledger facts of one outbound / inbound movement on a node (company-scoped).</summary>
public sealed record TraceMovementRef(string TransactionId, string? TransactionNumber, string MovementType, DateTimeOffset PostingDate,
    string SourceModule, string? SourceDocumentId);

/// <summary>The shipment read result on a Shipment node (display only — never part of the seal, never a quantity source).</summary>
public sealed record TraceShipmentInfo(ShipmentReadOutcome Read, string? Status, DateTimeOffset? ActualDeliverAt, string? ShipToReference);

/// <summary>One node of the trace. <see cref="Quantity"/> is in <see cref="BaseUomId"/> of the node's SKU.</summary>
public sealed class TraceNode
{
    public required string NodeKey { get; init; }

    public string? ParentKey { get; set; }

    public int Level { get; set; }

    public TraceSide Side { get; set; }

    public required TraceNodeKind Kind { get; init; }

    public string? LotId { get; init; }

    public string? LotNumber { get; init; }

    public Guid? ItemId { get; init; }

    public Guid? SkuId { get; init; }

    public SkuLevel? SkuLevel { get; init; }

    /// <summary>"Takip dışı": an input SKU that is not lot-controlled — never a gap (KR-D).</summary>
    public bool Untracked { get; init; }

    public string? WarehouseId { get; init; }

    public string? LocationId { get; init; }

    public StockStatus? StockStatus { get; init; }

    public decimal? Quantity { get; set; }

    public string? BaseUomId { get; init; }

    public TraceMovementRef? Movement { get; init; }

    public TraceShipmentInfo? Shipment { get; set; }

    /// <summary>The customer reference of a Shipment node: <c>shipToReference</c> when read, else <c>DOC:{sourceDocumentId}</c> (pack §8.6).</summary>
    public string? CustomerRef { get; set; }

    public List<string> Serials { get; init; } = [];

    public TraceGapCode? GapCode { get; init; }

    /// <summary>OTHER_COMPANY_ACTIVITY: the count only — never a lot, quantity or document of the other company (CT-R8).</summary>
    public long? Count { get; init; }

    public string? Text { get; init; }
}

/// <summary>A genealogy edge (parent → child) as walked; <see cref="Cycle"/> = the edge closes a loop (pack §8.2 step 2).</summary>
public sealed record TraceEdge(string From, string To, string? TransformationReference, string? QuantityConsumed, string? UomId, bool Cycle);

public sealed record TraceQuantity(Guid SkuId, string BaseUomId, decimal Quantity, int Count);

/// <summary>Pack §8.2 summary — per SKU + base unit, never a cross-unit total.</summary>
public sealed record TraceSummary(
    int FinishedGoodLots,
    IReadOnlyList<TraceQuantity> Stock,
    IReadOnlyList<TraceQuantity> Shipped,
    int InTransitShipments,
    int GapCount,
    long OtherCompanyActivityCount);

/// <summary>The trace result: ordered nodes (rule 10), edges, the summary and the seal of its canonical form (<c>rt-v1</c>).</summary>
public sealed record TraceResult(
    string RootLotId,
    string RootLotNumber,
    string Direction,
    DateTimeOffset AsOfUtc,
    DateTimeOffset ComputedAtUtc,
    long DurationMs,
    bool Truncated,
    int MaxDepth,
    int MaxNodes,
    IReadOnlyList<TraceNode> Nodes,
    IReadOnlyList<TraceEdge> Edges,
    TraceSummary Summary,
    string CanonicalVersion,
    string Sha256)
{
    public IEnumerable<TraceNode> Gaps => Nodes.Where(n => n.Kind == TraceNodeKind.Gap);
}
