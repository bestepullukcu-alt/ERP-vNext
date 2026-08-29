using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU20 — repository downtime / temporary controlled issue / escalation contracts. Tenant-scoped via the
// TenantRepository ExecutionFilter. There is deliberately NO delete method anywhere: a downtime log, an issue and
// an escalation are all regulated evidence, so cancellation and closure are status changes.

public interface IDocumentRepositoryDowntimeEventRepository
{
    Task<DocumentRepositoryDowntimeEvent> CreateAsync(DocumentRepositoryDowntimeEvent downtimeEvent, CancellationToken ct = default);
    Task<DocumentRepositoryDowntimeEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetByStatusAsync(DowntimeStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentRepositoryDowntimeEvent downtimeEvent, CancellationToken ct = default);
}

public interface IDocumentTemporaryControlledIssueRepository
{
    Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue issue, CancellationToken ct = default);
    Task<DocumentTemporaryControlledIssue?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid downtimeEventId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default);

    /// <summary>Issues that are issued/due but not yet reconciled — the overdue sweep candidates.</summary>
    Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentTemporaryControlledIssue issue, CancellationToken ct = default);
}

public interface IDocumentDowntimeEscalationRepository
{
    Task<DocumentDowntimeEscalation> CreateAsync(DocumentDowntimeEscalation escalation, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDowntimeEscalation>> GetByDowntimeEventAsync(Guid downtimeEventId, CancellationToken ct = default);
    Task<bool> UpdateAsync(DocumentDowntimeEscalation escalation, CancellationToken ct = default);
}
