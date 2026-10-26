using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>One balance bucket of an in-scope lot in the caller's company (on-hand &gt; 0) — a live "Eldeki stok" line.</summary>
public sealed record RecallStockBucket(
    string LotId, Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string? WarehouseId, string? LocationId, StockStatus Status, decimal OnHand, string BaseUomId);

/// <summary>One ledger movement as the recall reads it (KR-H return candidates).</summary>
public sealed record RecallLedgerMovement(
    string TransactionId,
    string? Number,
    MovementType Type,
    string? LotId,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    decimal BaseQuantity,
    string BaseUomId,
    StockStatus? ToStockStatus,
    DateTimeOffset PostingDate,
    DateTimeOffset PostedAtUtc,
    string? SourceDocumentId);

/// <summary>
/// MOD-0177 R3 — the in-process ledger reads of the recall's stock and returns tabs, ALWAYS in the caller's company (Tenant + LegalEntity).
/// Reads only: 0177 writes no movement (KR-H, pack §8.6).
/// </summary>
public interface IRecallLedgerReader
{
    Task<IReadOnlyList<RecallStockBucket>> StockOfLotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecallLedgerMovement>> CustomerReturnsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);

    Task<RecallLedgerMovement?> TransactionAsync(string transactionId, CancellationToken cancellationToken = default);
}
