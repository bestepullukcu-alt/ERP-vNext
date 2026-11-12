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
    BinClaim,

    /// <summary>DL-3: <c>(TenantId, LegalEntityId)</c> of <c>wms_count_settings</c> — one settings record per company.</summary>
    Settings,

    /// <summary>DL-5: <c>(TenantId, LegalEntityId, RequestedBy, IdempotencyKey)</c> of <c>wms_count_evidence_exports</c> — the export's key.</summary>
    EvidenceKey
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

/// <summary>DL-3 Variance Trace (pack §2.4, §8.13): the submitted lines of the caller's company, filtered; <c>All</c> = every match, no paging.</summary>
public sealed record CountVarianceLineQuery(Guid? WarehouseId, IReadOnlyCollection<CycleCountLineStatus> Statuses, DateTimeOffset? From, DateTimeOffset? To,
    Guid? SkuId, string? LotId, int Page, int PageSize, bool All = false);

public sealed record CycleCountLineSet(IReadOnlyList<CycleCountLine> Items, long Total);

/// <summary>
/// DL-4 dashboard (pack §8.13, AC-40) — counted at read time, never stored: open counts (Released / Counting / Submitted), lines awaiting a decision,
/// refused corrections, and the period's counted lines and exact lines (variance 0) for the accuracy.
/// </summary>
public sealed record CycleCountDashboardCounts(long OpenCounts, long AwaitingApprovalLines, long RefusedAdjustments, long CountedLines, long ExactLines);

/// <summary>A ledger movement as MOD-0181 reads it (pack §2.4: its source envelope as the caller declared it, its key, who and when) — a Find, never a write.</summary>
public sealed record CycleCountLedgerMovementRow(Guid TransactionId, string TransactionNumber, MovementType MovementType, Guid ItemId, Guid SkuId, SkuLevel SkuLevel,
    string? FromWarehouseId, string? FromLocationId, string? ToWarehouseId, string? ToLocationId, string? LotId, StockStatus? FromStockStatus,
    StockStatus? ToStockStatus, decimal Quantity, string UomId, decimal BaseQuantity, string SourceModule, string SourceType, string? SourceDocumentId,
    string? SourceLineId, string IdempotencyKey, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset TransactionDate, DateTimeOffset PostingDate);

/// <summary>Pack §2.4: the <c>COUNT_*</c> movements on <see cref="BinIds"/> created in <c>[From, To)</c> — bin indexes (COUNT_GAIN → to, COUNT_LOSS → from).</summary>
/// <summary>DL-5: one page of the company's Evidence Pack export log (newest first), optionally of one count.</summary>
public sealed record CountEvidenceExportPage(IReadOnlyList<CountEvidenceExport> Items, long Total);

/// <summary>DL-5: a location's code and name (the Evidence Pack names the bins — pack §4.5; read with <c>Find</c> only).</summary>
public sealed record CycleCountLocationName(Guid Id, string Code, string Name);

public sealed record CycleCountManualRequest(IReadOnlyCollection<Guid> BinIds, DateTimeOffset From, DateTimeOffset To);

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

    // ── DL-3 ──

    /// <summary>A line of the caller's company by its id alone (the <c>/count-variances/{lineId}</c> family) — its count may be soft-deleted.</summary>
    Task<CycleCountLine?> FindLineByIdAsync(Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>The lines of the caller's company among <paramref name="lineIds"/>.</summary>
    Task<IReadOnlyList<CycleCountLine>> FindLinesAsync(IReadOnlyCollection<Guid> lineIds, CancellationToken cancellationToken = default);

    /// <summary>Pack §8.13 Variance Trace (source <c>count</c>): the submitted lines (a variance is written) — newest entry first.</summary>
    Task<CycleCountLineSet> VarianceLinesAsync(CountVarianceLineQuery query, CancellationToken cancellationToken = default);

    /// <summary>DL-4 (AC-40): the dashboard's counts of the caller's company (one warehouse when given); the period starts at <paramref name="periodStart"/>.</summary>
    Task<CycleCountDashboardCounts> DashboardCountsAsync(Guid? warehouseId, DateTimeOffset periodStart, CancellationToken cancellationToken = default);

    /// <summary>Pack §4.4: the company's settings record, or null (the defaults hold).</summary>
    Task<CycleCountSettings?> FindSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Pack §2.4: every location node of the warehouse (its subtree, any status — a movement may name a node retired since).</summary>
    Task<IReadOnlyList<Guid>> WarehouseNodesAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Pack §2.4 (Q-21 = A): the <c>COUNT_*</c> movements on the bins in the window — the ledger's collection, <c>Find</c> only.</summary>
    Task<IReadOnlyList<CycleCountLedgerMovementRow>> CountMovementsAsync(CycleCountManualRequest request, CancellationToken cancellationToken = default);

    /// <summary>The ledger movements holding these idempotency keys (the unique key index) — <c>Find</c> only.</summary>
    Task<IReadOnlyList<CycleCountLedgerMovementRow>> MovementsByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>One ledger movement of the caller's company by id (the resolve's "same correction" check reads its type) — <c>Find</c> only.</summary>
    Task<CycleCountLedgerMovementRow?> LedgerMovementAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CycleCountEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default);

    // ── DL-5 Evidence Pack (pack §4.5): the export log and the stored bytes, always of the caller's tenant + company (another one's is 404) ──

    /// <summary>The caller's export with this idempotency key (the replay rule).</summary>
    Task<CountEvidenceExport?> FindEvidenceExportByKeyAsync(Guid requestedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<CountEvidenceExport?> FindEvidenceExportAsync(Guid exportId, CancellationToken cancellationToken = default);

    Task<CountEvidenceExportPage> ListEvidenceExportsAsync(Guid? countId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The stored bytes of an export (what was hashed).</summary>
    Task<CountEvidenceFile?> FindEvidenceFileAsync(Guid exportId, CancellationToken cancellationToken = default);

    /// <summary>Codes and names of locations of the caller's company (bins, warehouse) — <c>Find</c> only.</summary>
    Task<IReadOnlyList<CycleCountLocationName>> LocationNamesAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// DL-3 recount (pack §4.7): moves the OPEN claims of <paramref name="fromCountId"/> on <paramref name="binIds"/> to <paramref name="toCountId"/>
    /// (<c>TransferredFromCountId</c> written, still open) and returns the bins moved; a bin whose claim is not the source's is not moved.
    /// </summary>
    Task<IReadOnlyList<Guid>> TransferBinClaimsAsync(Guid fromCountId, Guid toCountId, IReadOnlyCollection<Guid> binIds, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>DL-3: the company's settings record (the unique company index refuses a second one).</summary>
    Task InsertSettingsAsync(CycleCountSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Replaces the settings if their stored version is still <paramref name="expectedVersion"/>; false = 0 records.</summary>
    Task<bool> ReplaceSettingsAsync(CycleCountSettings settings, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>DL-5: the export record and its bytes, both insert-only (a second export with the caller's key is <see cref="CycleCountUniqueIndex.EvidenceKey"/>).</summary>
    Task InsertEvidenceExportAsync(CountEvidenceExport export, CountEvidenceFile file, CancellationToken cancellationToken = default);

    /// <summary>Sets the count's own open claims to <c>Open = false</c> (soft; pack §4.7) and returns how many it released.</summary>
    Task<long> ReleaseBinClaimsAsync(Guid countId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack §8.2 rule 4 — IN this snapshot transaction: (a) the scope's stockable Active bins and their AVAILABLE balance rows with stock,
    /// then (b) the ids of the movements on those bins created at or after the window start. Every ledger posting is either wholly visible
    /// (balance + id) or not at all. A read that cannot be done is <see cref="CycleCountInventoryUnavailableException"/>.
    /// </summary>
    Task<CycleCountScopeSnapshot> ReadScopeSnapshotAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One approved line whose correction is still Pending (pack §8.6 sweeper) — with the scope and the actor its command runs as.</summary>
public sealed record CycleCountPendingRow(Guid TenantId, Guid LegalEntityId, Guid LineId, Guid ActorUserId);

/// <summary>
/// The sweeper's scan (pack §8.6, MOD-0178 <c>IWarehousePendingScan</c> pattern): ACROSS tenants, read only, the approved lines whose correction is
/// Pending and whose last attempt is older than the given age. It writes nothing and opens no transaction; each row's command runs in a fresh
/// scope of its own tenant + company.
/// </summary>
public interface ICycleCountPendingScan
{
    Task<IReadOnlyList<CycleCountPendingRow>> PendingAsync(DateTimeOffset lastAttemptBefore, int limit, IReadOnlyCollection<Guid>? onlyTenants,
        CancellationToken cancellationToken = default);
}
