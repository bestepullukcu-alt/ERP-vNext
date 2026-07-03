using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// FEAT-NAVPREFS-DOMAINS — store for a tenant's per-domain sidebar preferences. The full set is read/replaced at
/// once (the UI submits the entire set), so there is no per-domain mutation surface.
/// </summary>
public interface ITenantNavDomainPreferenceRepository
{
    Task<IReadOnlyList<TenantNavDomainPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task ReplaceForTenantAsync(Guid tenantId, IReadOnlyCollection<TenantNavDomainPreference> items, CancellationToken ct = default);
}
