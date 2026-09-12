using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) — eligibility policy master. Tenant scoped, soft-delete aware. No delete method: closing a
/// policy is the soft archive lifecycle. <c>PolicyCode</c> is shared across versions; a duplicate ACTIVE (non-archived)
/// policy code is guarded on create.
/// </summary>
public interface IEligibilityPolicyRepository
{
    Task<EligibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EligibilityPolicy>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EligibilityPolicy>> ListByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken);
    Task<EligibilityPolicy?> GetActiveByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken);
    Task InsertAsync(EligibilityPolicy entity, CancellationToken cancellationToken);
    Task UpdateAsync(EligibilityPolicy entity, CancellationToken cancellationToken);
}
