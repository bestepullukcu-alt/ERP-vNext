using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// The single persistence seam of MOD-0173 (pack §10: a specific repository where atomic transaction + projection is
/// required — no generic repository that could drop the Tenant/LE predicate). Every member is implicitly scoped to
/// the caller's resolved Tenant + Legal Entity.
///
/// ⚠ APPEND-ONLY BY SHAPE (pack §8, AC17): there is deliberately no member that updates, replaces or deletes a
/// transaction or edits a balance row. The only write is <see cref="PostAsync"/>. An architecture test pins this.
/// </summary>
public interface IInventoryLedgerRepository
{
    Task<InventoryTransaction?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<InventoryTransaction?> FindTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<bool> IsReversedAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically: assigns the transaction number, applies every leg under its coverage condition, inserts the
    /// transaction (with its result snapshot), the audit record and the Pending outbox event — one Mongo
    /// transaction, no partial success.
    /// </summary>
    Task<LedgerPostingResult> PostAsync(LedgerPosting posting, CancellationToken cancellationToken = default);

    Task<BalancePage> QueryBalancesAsync(BalanceQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryBalance>> ListBalancesAsync(AvailabilityFilter filter, CancellationToken cancellationToken = default);

    Task<TransactionPage> QueryTransactionsAsync(TransactionQuery query, CancellationToken cancellationToken = default);
}
