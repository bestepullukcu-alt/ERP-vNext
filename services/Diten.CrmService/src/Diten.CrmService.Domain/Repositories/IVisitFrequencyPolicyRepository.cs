using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0165 FU03 Visit Frequency / Call-Cycle Policy master. Tenant scoped and soft-delete aware. There is
/// deliberately <b>no delete method</b>: closing a policy is a status update to inactive/archived, so history stays
/// readable. The resolve provider reads through <see cref="ListActiveByTargetsAsync"/> and performs no writes.
/// </summary>
public interface IVisitFrequencyPolicyRepository
{
    Task<VisitFrequencyPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted policies of a tenant (any status), newest first. Callers filter as needed; archived
    /// rows are included because read/history must still show them.</summary>
    Task<IReadOnlyList<VisitFrequencyPolicy>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived policy carrying <paramref name="policyCode"/> (used for the
    /// duplicate-code guard). Archived codes are reusable.</summary>
    Task<VisitFrequencyPolicy?> GetActiveByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken);

    /// <summary>Read-only resolve seam: active policies whose (TargetType, TargetId) is one of the requested target
    /// pairs. Effective-window and business-scope filtering are done in memory by the resolve engine (EffectiveFrom /
    /// EffectiveTo are DateTimeOffset — stored as a BSON array — so they are never part of a compound index or a
    /// server-side sort).</summary>
    Task<IReadOnlyList<VisitFrequencyPolicy>> ListActiveByTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> targetIds, CancellationToken cancellationToken);

    Task InsertAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken);

    Task UpdateAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken);
}
