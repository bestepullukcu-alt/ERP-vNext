using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.CycleCounting.Repositories;

public sealed record CycleCountQuery(IReadOnlyCollection<CycleCountStatus> Statuses, Guid? WarehouseId, string? Search, int Page, int PageSize);

public sealed record CycleCountPage(IReadOnlyList<CycleCount> Items, long Total);

public sealed record CycleCountLinePage(IReadOnlyList<CycleCountLine> Items, long Total);

public sealed record CycleCountEventPage(IReadOnlyList<CycleCountEvent> Items, long Total);

/// <summary>What a write transaction did: the number of counts it changed and the entity the central record names (null for a bulk write).</summary>
public sealed record CycleCountWriteOutcome(int Affected, Guid? EntityId);

/// <summary>The unique indexes a MOD-0181 write can hit (pack §4.6).</summary>
public enum CycleCountUniqueIndex
{
    /// <summary><c>(TenantId, Number)</c>.</summary>
    Number,

    /// <summary><c>(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)</c>.</summary>
    CreateKey,

    /// <summary>An index of <c>wms_count_events</c> (count + seq, or a key event).</summary>
    Event,

    /// <summary>The line key of a count.</summary>
    Line,

    /// <summary>The partial unique index on an open bin claim (F-6): the bin is taken by another open count.</summary>
    BinClaim
}

public sealed class CycleCountDuplicateException(CycleCountUniqueIndex index, Exception? inner = null) : Exception($"Duplicate on {index}.", inner)
{
    public CycleCountUniqueIndex Index { get; } = index;
}

/// <summary>The release's snapshot read could not be done (pack §8.2 rule 4 "Kapalı-başarısız") — 503, nothing written.</summary>
public sealed class CycleCountInventoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What the release's snapshot transaction reads (pack §8.2 rule 4): the scope and the window start of the movement-id read.</summary>
public sealed record CycleCountScopeRequest(Guid WarehouseId, bool Full, IReadOnlyCollection<Guid> ScopeLocationIds, IReadOnlyCollection<Guid> ScopeSkuIds,
    DateTimeOffset WindowStart);

/// <summary>One AVAILABLE balance row with stock on a scope bin, as the snapshot saw it.</summary>
public sealed record CycleCountSnapshotRow(Guid LocationId, Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string? LotId, StockStatus StockStatus, decimal OnHand,
    string BaseUomId);

/// <summary>The snapshot: the scope bins, their balance rows and the ids of the window's movements on them — all from ONE snapshot transaction.</summary>
public sealed record CycleCountScopeSnapshot(IReadOnlyList<Guid> BinIds, IReadOnlyList<CycleCountSnapshotRow> Rows, IReadOnlyList<Guid> TransactionIds);

/// <summary>The key of a count line (pack §4.2): bin, SKU, level, lot, stock status.</summary>
public sealed record CycleCountLineKey(Guid LocationId, Guid SkuId, SkuLevel SkuLevel, string? LotId, StockStatus StockStatus)
{
    public static CycleCountLineKey Of(CycleCountLine line) => new(line.LocationId, line.SkuId, line.SkuLevel, line.LotId, line.StockStatus);
}

/// <summary>What the submit's snapshot read covers (pack §8.4): the count's scope bins and the window start <c>SnapshotAt − W</c>.</summary>
public sealed record CycleCountLedgerRequest(IReadOnlyCollection<Guid> BinIds, DateTimeOffset WindowStart);

/// <summary>One balance leg of a ledger movement on a scope bin (the ledger's own leg rule: source −BaseQuantity, target +BaseQuantity).</summary>
public sealed record CycleCountLedgerLeg(CycleCountLineKey Key, decimal Delta);

/// <summary>A window movement on the scope bins: its id, its <c>CreatedAt</c> (never <c>PostingDate</c> — pack §8.4) and its legs on those bins.</summary>
public sealed record CycleCountLedgerMovement(Guid TransactionId, DateTimeOffset CreatedAt, IReadOnlyList<CycleCountLedgerLeg> Legs);

/// <summary>
/// The submit's ledger read — from ONE snapshot transaction: the window's movements on the scope bins and the scope's balance today per line key
/// (every AVAILABLE row of those bins, zero and negative included, summed over owner / project).
/// </summary>
public sealed record CycleCountLedgerRead(IReadOnlyList<CycleCountLedgerMovement> Movements, IReadOnlyDictionary<CycleCountLineKey, decimal> BalanceToday);

/// <summary>
/// MOD-0181 counts, lines, timeline and bin claims (pack §3, §4). Every member is bound to the caller's resolved Tenant + LegalEntity — another
/// company's or tenant's count does not exist here (404). Changes go through <see cref="InTransactionAsync"/>.
/// </summary>
public interface ICycleCountRepository
{
    /// <summary>A live (not soft-deleted) count of the caller's company.</summary>
    Task<CycleCount?> FindAsync(Guid countId, CancellationToken cancellationToken = default);

    /// <summary>The count even when a Draft cancel deleted it softly (the replay of that cancel answers with it).</summary>
    Task<CycleCount?> FindIncludingDeletedAsync(Guid countId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CycleCount>> FindManyAsync(IReadOnlyCollection<Guid> countIds, CancellationToken cancellationToken = default);

    Task<CycleCount?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<CycleCountPage> ListAsync(CycleCountQuery query, CancellationToken cancellationToken = default);

    Task<CycleCountLinePage> LinesAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CycleCountEventPage> EventsAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>A line of a live count of the caller's company.</summary>
    Task<CycleCountLine?> FindLineAsync(Guid countId, Guid lineId, CancellationToken cancellationToken = default);

    Task<CycleCountEvent?> FindKeyEventAsync(Guid countId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CycleCountEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The SKUs with AVAILABLE stock in the scope, read OUTSIDE any transaction — only so the release can fetch their PRODUCT-MASTER flags before
    /// its snapshot transaction opens. A read that cannot be done is <see cref="CycleCountInventoryUnavailableException"/>.
    /// </summary>
    Task<IReadOnlyList<(Guid ItemId, Guid SkuId, SkuLevel Level)>> ScopeSkusAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction (<c>ReadConcern.Snapshot</c>, majority); the central-audit event is
    /// enlisted only when the work changed a count (<see cref="CycleCountWriteOutcome.Affected"/> &gt; 0). A unique-index refusal surfaces as
    /// <see cref="CycleCountDuplicateException"/>.
    /// </summary>
    Task<CycleCountWriteOutcome> InTransactionAsync(Func<ICycleCountWriteSession, CancellationToken, Task<CycleCountWriteOutcome>> work,
        CancellationToken cancellationToken = default);
}

public interface ICycleCountWriteSession
{
    Task InsertCountAsync(CycleCount count, CancellationToken cancellationToken = default);

    /// <summary>Replaces the count if its stored version is still <paramref name="expectedVersion"/>; false = 0 records (a concurrent write won).</summary>
    Task<bool> ReplaceCountAsync(CycleCount count, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertLinesAsync(IReadOnlyList<CycleCountLine> lines, CancellationToken cancellationToken = default);

    /// <summary>Replaces each line if its stored version is still the paired one; false = at least one was not (the transaction is then abandoned).</summary>
    Task<bool> ReplaceLinesAsync(IReadOnlyList<(CycleCountLine Line, int ExpectedVersion)> lines, CancellationToken cancellationToken = default);

    /// <summary>Every line of the count, read in this transaction.</summary>
    Task<IReadOnlyList<CycleCountLine>> ReadLinesAsync(Guid countId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §8.4 — IN this snapshot transaction: the window's movements on <see cref="CycleCountLedgerRequest.BinIds"/> and the balance today of
    /// those bins. A read that cannot be done is <see cref="CycleCountInventoryUnavailableException"/> (503, the submit writes nothing — AC-20).
    /// </summary>
    Task<CycleCountLedgerRead> ReadVarianceLedgerAsync(CycleCountLedgerRequest request, CancellationToken cancellationToken = default);

    Task InsertEventsAsync(IReadOnlyList<CycleCountEvent> events, CancellationToken cancellationToken = default);

    Task InsertBinClaimsAsync(IReadOnlyList<CycleCountBinClaim> claims, CancellationToken cancellationToken = default);

    /// <summary>Sets the count's own open claims to <c>Open = false</c> (soft; pack §4.7) and returns how many it released.</summary>
    Task<long> ReleaseBinClaimsAsync(Guid countId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §8.2 rule 4 — IN this snapshot transaction: (a) the scope's stockable Active bins and their AVAILABLE balance rows with stock,
    /// then (b) the ids of the movements on those bins created at or after the window start. Every ledger posting is either wholly visible
    /// (balance + id) or not at all. A read that cannot be done is <see cref="CycleCountInventoryUnavailableException"/>.
    /// </summary>
    Task<CycleCountScopeSnapshot> ReadScopeSnapshotAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default);
}
