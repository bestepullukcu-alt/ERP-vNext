using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — the persistence under the in-transaction audit door. Only
/// <c>CanonicalTransactionalAuditOutboxWriter</c> calls it: handlers depend on
/// <see cref="ITransactionalAuditOutboxWriter"/>, whose one production implementation builds the canonical payload
/// first. The store refuses a payload the outbox mapper could not turn into an <c>audit_events</c> row, so a row that
/// would dead-letter fails the transaction instead of being written.
/// </summary>
public interface ITransactionalAuditOutboxStore
{
    Task<bool> TryInsertAsync(
        IPlatformTransactionSession session,
        AuditOutboxWriteRequest request,
        CancellationToken ct = default);
}
