using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Repositories;

public sealed record HoldQuery(
    string? Search,
    string? WarehouseId,
    StockStatus? StockStatus,
    HoldSource? Source,
    HoldStatus? Status,
    HoldReviewState? ReviewState,
    string? LotId,
    DateTimeOffset? EnteredBefore,
    int Page,
    int PageSize);

public sealed record HoldPage(IReadOnlyList<QuarantineHold> Items, long Total);

public sealed record BlockedQuery(
    string? Search,
    string? BlockReasonCode,
    bool PendingOnly,
    BlockedOrigin? Origin,
    BlockedStatus? Status,
    string? WarehouseId,
    int Page,
    int PageSize);

public sealed record BlockedPage(IReadOnlyList<BlockedStock> Items, long Total);

/// <summary>A write refused by a unique index (intake source movement, a create key, a once-only decision event).</summary>
public sealed class QuarantineDuplicateException(string index) : Exception($"Duplicate on {index}.")
{
    public string Index { get; } = index;
}

/// <summary>
/// MOD-0175 records except decisions (pack §10). Every member is scoped to the caller's resolved Tenant + Legal Entity
/// (block reasons: Tenant). Multi-record changes go through <see cref="InTransactionAsync{T}"/> — one Mongo transaction,
/// all or nothing. There is no delete anywhere: holds, blocked records and history are retained (contract §8).
/// </summary>
public interface IQuarantineRepository
{
    Task<QuarantineHold?> FindHoldAsync(Guid holdId, CancellationToken cancellationToken = default);

    Task<QuarantineHold?> FindHoldBySourceMovementAsync(string transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarantineHold>> FindHoldsByCreateKeysAsync(Guid createdBy, IReadOnlyCollection<string> createKeys, CancellationToken cancellationToken = default);

    Task<HoldPage> ListHoldsAsync(HoldQuery query, CancellationToken cancellationToken = default);

    Task<BlockedStock?> FindBlockedAsync(Guid blockedId, CancellationToken cancellationToken = default);

    Task<BlockedStock?> FindBlockedBySourceMovementAsync(string transactionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlockedStock>> FindBlockedByCreateKeysAsync(Guid createdBy, IReadOnlyCollection<string> createKeys, CancellationToken cancellationToken = default);

    Task<BlockedPage> ListBlockedAsync(BlockedQuery query, CancellationToken cancellationToken = default);

    /// <summary>V23: the id names a hold / blocked record / decision of ANOTHER company of the caller's tenant (→ 403, not 404).</summary>
    Task<bool> ExistsInAnotherCompanyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarantineHistoryEntry>> HistoryAsync(HistorySubject subject, Guid subjectId, CancellationToken cancellationToken = default);

    Task<QuarantineSettings?> FindSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>The tenant's block reasons; the five CT-Q8 seeds are inserted once per tenant on first read.</summary>
    Task<IReadOnlyList<BlockReason>> BlockReasonsAsync(CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<IQuarantineWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// The writes of one MOD-0175 transaction. Decisions, decision events and history rows are INSERT-ONLY by shape: this
/// session has an insert for each and nothing that updates, replaces or deletes them (architecture-tested, §16 Q1).
/// </summary>
public interface IQuarantineWriteSession
{
    /// <summary>Next <c>{prefix}-{yyyy}-{nnnn}</c> of the tenant (atomic counter).</summary>
    Task<string> NextNumberAsync(string prefix, int year, CancellationToken cancellationToken = default);

    Task InsertHoldAsync(QuarantineHold hold, CancellationToken cancellationToken = default);

    Task<bool> ReplaceHoldAsync(QuarantineHold hold, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertBlockedAsync(BlockedStock blocked, CancellationToken cancellationToken = default);

    Task<bool> ReplaceBlockedAsync(BlockedStock blocked, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertDecisionAsync(QuarantineDecision decision, CancellationToken cancellationToken = default);

    Task InsertDecisionEventAsync(QuarantineDecisionEvent decisionEvent, CancellationToken cancellationToken = default);

    Task InsertHistoryAsync(IReadOnlyList<QuarantineHistoryEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>The decision's events, read inside this transaction (state checks that must not race).</summary>
    Task<IReadOnlyList<QuarantineDecisionEvent>> DecisionEventsAsync(Guid decisionId, CancellationToken cancellationToken = default);

    Task<bool> SaveSettingsAsync(QuarantineSettings settings, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertBlockReasonAsync(BlockReason reason, CancellationToken cancellationToken = default);

    Task<bool> ReplaceBlockReasonAsync(BlockReason reason, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Stamps the first use of a block-reason code (it is locked from then on, V20).</summary>
    Task MarkBlockReasonUsedAsync(string code, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quality decisions and their events — INSERT-ONLY (pack §3/§4: a decision is immutable after submit; it is the signed
/// subject). Reads only; the inserts are on <see cref="IQuarantineWriteSession"/>. No member here may update, replace or
/// delete (architecture test + K3 sabotage (a)).
/// </summary>
public interface IQuarantineDecisionRepository
{
    Task<QuarantineDecision?> FindAsync(Guid decisionId, CancellationToken cancellationToken = default);

    Task<QuarantineDecision?> FindByIdempotencyKeyAsync(Guid deciderUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarantineDecision>> ListByTargetAsync(Guid targetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuarantineDecisionEvent>> EventsAsync(IReadOnlyCollection<Guid> decisionIds, CancellationToken cancellationToken = default);
}
