using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — content-set (assembly draft) master. Tenant scoped, soft-delete aware. No delete method:
/// closing a set is the soft archive lifecycle. <c>SetCode</c> is unique per tenant among non-archived rows.
/// </summary>
public interface IContentSetRepository
{
    Task<ContentSet?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentSet>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ContentSet?> GetActiveByCodeAsync(Guid tenantId, string setCode, CancellationToken cancellationToken);
    Task InsertAsync(ContentSet entity, CancellationToken cancellationToken);
    Task UpdateAsync(ContentSet entity, CancellationToken cancellationToken);
}
