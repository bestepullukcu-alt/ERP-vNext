using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0162 FU02 audience-profile master. Tenant scoped and soft-delete aware. <b>No delete method</b>: closing a
/// profile is the soft archive lifecycle.
/// </summary>
public interface IAudienceProfileRepository
{
    Task<AudienceProfile?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The first non-deleted, non-archived profile carrying <paramref name="profileCode"/> (duplicate-code
    /// guard). An archived code is reusable.</summary>
    Task<AudienceProfile?> GetActiveByCodeAsync(
        Guid tenantId, string profileCode, CancellationToken cancellationToken);

    Task InsertAsync(AudienceProfile profile, CancellationToken cancellationToken);

    Task UpdateAsync(AudienceProfile profile, CancellationToken cancellationToken);
}
