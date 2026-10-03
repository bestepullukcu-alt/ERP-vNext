using Diten.Platform.Domain.Entities.Audit;

namespace Diten.Platform.Domain.Repositories;

public interface IAuditEventRepository
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid id, CancellationToken ct = default);
    Task<AuditEventSearchResult> SearchForPlatformCrossTenantAsync(AuditEventSearchRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default);

    /// <summary>
    /// INTX FIX2 — whether the outbox message with this idempotency key has already become an audit event (in the
    /// current read tenant). The outbox worker's duplicate check: one indexed lookup, not every event of a correlation.
    /// </summary>
    Task<bool> ExistsByOutboxIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    // Phase 2 IAuditService must wrap this raw cross-tenant query and meta-audit the access.
    Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdForPlatformCrossTenantAsync(Guid correlationId, CancellationToken ct = default);
    Task<int> RedactActorPiiForPlatformCrossTenantAsync(AuditActorPiiRedactionRequest request, CancellationToken ct = default);
}
