using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// WP-CL-BE-1 (claims v2) — claim country versions. Tenant scoped, soft-delete aware. No delete method: closing a
/// version is the archive lifecycle.
/// </summary>
public interface IClaimCountryVersionRepository
{
    Task<ClaimCountryVersion?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Every version of every country for the logical claim (all core versions).</summary>
    Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(
        Guid tenantId, string claimCode, CancellationToken cancellationToken);

    /// <summary>Versions bound to one claim record.</summary>
    Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(
        Guid tenantId, Guid claimId, CancellationToken cancellationToken);

    /// <summary>All versions of the tenant (coverage matrix / list summary).</summary>
    Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task InsertAsync(ClaimCountryVersion entity, CancellationToken cancellationToken);
    Task UpdateAsync(ClaimCountryVersion entity, CancellationToken cancellationToken);
}
