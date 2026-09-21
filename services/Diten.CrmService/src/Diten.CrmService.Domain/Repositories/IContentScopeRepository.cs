using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — content-scope master. Tenant scoped, soft-delete aware. No delete method: closing a scope
/// is the soft archive lifecycle. <c>ScopeCode</c> is shared across versions; a duplicate ACTIVE (non-archived) scope
/// code is guarded on create.
/// </summary>
public interface IContentScopeRepository
{
    Task<ContentScope?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentScope>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ContentScope?> GetActiveByCodeAsync(Guid tenantId, string scopeCode, CancellationToken cancellationToken);
    Task InsertAsync(ContentScope entity, CancellationToken cancellationToken);
    Task UpdateAsync(ContentScope entity, CancellationToken cancellationToken);
}
