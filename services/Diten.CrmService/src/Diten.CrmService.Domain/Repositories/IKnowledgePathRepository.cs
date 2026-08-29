using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU04 KnowledgePath master — the ONLY repository of this FU (D2: steps are embedded, no second collection).
/// Tenant scoped and soft-delete aware. There is deliberately <b>no delete method</b>: closing a path (or a step, in the
/// same document) is the soft archive lifecycle, so path/step history stays readable. Every write is a single-document
/// replace guarded by the optimistic <see cref="EntityBase.Version"/> token, so no multi-document transaction is needed.
/// </summary>
public interface IKnowledgePathRepository
{
    Task<KnowledgePath?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted paths of a tenant (any status, archived included — history must stay readable).</summary>
    Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Non-deleted paths carrying <paramref name="pathCode"/> (any version/status). Backs the duplicate
    /// code+version guard (V-P03) and the overlapping-published guard (V-P10).</summary>
    Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid tenantId, string pathCode, CancellationToken cancellationToken);

    Task InsertAsync(KnowledgePath entity, CancellationToken cancellationToken);

    /// <summary>Version-checked single-document replace. Bumps <see cref="EntityBase.Version"/> from
    /// <paramref name="expectedVersion"/> to <c>expectedVersion + 1</c>; returns false when another writer already moved
    /// the token (controlled 409, no silent overwrite).</summary>
    Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken cancellationToken);
}
