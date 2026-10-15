using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Trace;

namespace Diten.InventoryService.Application.Features.Recall.Ports;

public sealed record TraceLot(Guid Id, string LotNumber, Guid ItemId, Guid SkuId, SkuLevel SkuLevel, bool Serialized);

public sealed record TraceLink(Guid ParentLotId, Guid ChildLotId, string? TransformationReference, string? QuantityConsumed, string? UomId);

public sealed record TraceTransaction(
    string Id,
    string? Number,
    MovementType Type,
    string? LotId,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    decimal BaseQuantity,
    string BaseUomId,
    string SourceModule,
    string? SourceDocumentId,
    DateTimeOffset PostingDate,
    string? ReversesTransactionId,
    IReadOnlyList<string> SerialIds);

/// <summary>
/// WP-0177-R7a — one GOODS_ISSUE_SALES movement that stands (not reversed) behind a scope Shipment item, as the case's customer rows show it.
/// <see cref="IsShipmentDocument"/>: its document is an MVP-6 shipment — the trace's own node identification (<see cref="TraceRules.ShipmentModule"/>),
/// made here so no other recall type reads the source module (R2 rule); only those documents are re-read from MVP-6.
/// </summary>
public sealed record TraceSalesIssue(string TransactionId, string? Number, string LotId, decimal BaseQuantity, string BaseUomId, DateTimeOffset PostingDate,
    string? DocumentId, bool IsShipmentDocument);

public sealed record TraceBalance(string LotId, Guid ItemId, Guid SkuId, string? WarehouseId, string? LocationId, StockStatus Status, decimal OnHand, string BaseUomId);

public sealed record TraceSerial(Guid LotId, string Number, string? WarehouseId, string? LocationId, bool Active);

/// <summary>
/// Pack §8.2 — the in-process reads of the trace engine (no HTTP inside InventoryService; the 2-level HTTP <c>getGenealogy</c> is NOT used).
/// CT-R8 company rule: lots and genealogy links are TENANT-wide (MOD-0174 CT-D3); every ledger read (transactions, balances, first
/// inbound) is the CALLER'S COMPANY only; the other companies are seen as ONE count (<see cref="OtherCompanyActivityAsync"/>), never
/// as lots, quantities or documents. Every list member takes a whole batch (one query per level / batch).
/// </summary>
public interface IRecallTraceSource
{
    Task<IReadOnlyList<TraceLot>> LotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraceLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraceLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default);

    /// <summary>The first inbound movement of each lot in the caller's company (the lot's source).</summary>
    Task<IReadOnlyDictionary<Guid, TraceTransaction>> FirstInboundAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    /// <summary>Every movement of these lots in the caller's company (index <c>ix_tenant_le_lot</c>).</summary>
    Task<IReadOnlyList<TraceTransaction>> TransactionsOfLotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    /// <summary>WP-0177-R7a — the sales issues of the lots that stand (net of reversals), the caller's company ONLY (getCaseCustomers).</summary>
    Task<IReadOnlyList<TraceSalesIssue>> SalesIssuesOfLotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);

    /// <summary>The balance rows (on-hand &gt; 0) of these lots in the caller's company.</summary>
    Task<IReadOnlyList<TraceBalance>> BalancesAsync(IReadOnlyCollection<Guid> itemIds, IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    /// <summary>The movements of one source document in the caller's company (production inputs — new index, pack §8.9).</summary>
    Task<IReadOnlyList<TraceTransaction>> TransactionsOfDocumentAsync(string sourceModule, string sourceDocumentId, CancellationToken cancellationToken = default);

    /// <summary>The outbound movements of one SKU in a posting window in the caller's company, any lot (gap rules; <c>ix_tenant_le_item_postingdate</c>).</summary>
    Task<IReadOnlyList<TraceTransaction>> OutboundOfSkuAsync(Guid itemId, Guid skuId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Typed-lot movements re-keyed by the MOD-0174 migration: transaction id → registered lot.</summary>
    Task<IReadOnlyDictionary<string, Guid>> TypedLotLinksAsync(IReadOnlyCollection<string> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>CT-R8 — how many movements of these lots exist in OTHER companies of the tenant (a count, nothing else).</summary>
    Task<long> OtherCompanyActivityAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraceSerial>> SerialsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    /// <summary>PRODUCT-MASTER getSku stock behaviour; null = unknown (treated as lot-controlled: the conservative side).</summary>
    Task<bool?> LotControlledAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pack §8.6 — the MVP-6 shipment read (<c>GET /api/shipment-bundle/shipments/{id}</c> through the Gateway with the caller's token and
/// company). No cache; 2 s per read; at most 8 in parallel. Every outcome is an answer, never an exception: the trace always continues.
/// </summary>
public interface IShipmentReader
{
    Task<IReadOnlyDictionary<string, TraceShipmentInfo>> ReadAsync(IReadOnlyCollection<string> shipmentIds, CancellationToken cancellationToken = default);
}
