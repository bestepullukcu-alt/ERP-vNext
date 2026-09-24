using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-15 (CAND-CAP-0011) — ContentSetRevision (frozen manifest) store. Tenant scoped, soft-delete aware, no delete
/// method (archive-only). Revisions are immutable except for the review status/decision transition; the lineage per
/// <c>ContentSetId</c> drives both the next revision number and the idempotent open-revision lookup.
/// </summary>
public interface IContentSetRevisionRepository
{
    Task<ContentSetRevision?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All revisions of one content set, newest-first (by RevisionNumber desc).</summary>
    Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(Guid tenantId, Guid contentSetId, CancellationToken cancellationToken);

    Task InsertAsync(ContentSetRevision entity, CancellationToken cancellationToken);

    Task UpdateAsync(ContentSetRevision entity, CancellationToken cancellationToken);
}
