using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-KP-2 — knowledge path revisions (<c>knowledge_path_revisions</c>). Tenant scoped; no delete (a revision is history).
/// Every update is a single-document replace guarded by the optimistic <see cref="EntityBase.Version"/> token; the
/// (tenant, path, revision number) pair is unique in the store, so two concurrent submissions cannot share a number.
/// </summary>
public interface IKnowledgePathRevisionRepository
{
    Task<KnowledgePathRevision?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>The revisions of one path version, oldest first (RevisionNumber).</summary>
    Task<IReadOnlyList<KnowledgePathRevision>> ListByPathAsync(Guid tenantId, Guid pathId, CancellationToken cancellationToken);

    Task InsertAsync(KnowledgePathRevision entity, CancellationToken cancellationToken);

    /// <summary>Replaces the revision when its stored Version equals <paramref name="expectedVersion"/>; bumps it.</summary>
    Task<bool> ReplaceAsync(KnowledgePathRevision entity, int expectedVersion, CancellationToken cancellationToken);
}
