using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-12 (CAND-CAP-0011) — claim master. Tenant scoped, soft-delete aware. No delete method: closing a claim is the
/// soft archive lifecycle. <c>ClaimCode</c> is shared across versions; a duplicate ACTIVE (non-archived) claim code is
/// guarded on create.
/// </summary>
public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid tenantId, string claimCode, CancellationToken cancellationToken);
    Task<Claim?> GetActiveByCodeAsync(Guid tenantId, string claimCode, CancellationToken cancellationToken);
    Task InsertAsync(Claim entity, CancellationToken cancellationToken);
    Task UpdateAsync(Claim entity, CancellationToken cancellationToken);
}
