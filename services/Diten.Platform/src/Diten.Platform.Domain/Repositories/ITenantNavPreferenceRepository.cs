using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// FEAT-TENANT-NAV-PREFS — store for a tenant's sidebar module preferences. The full set is read/replaced at once
/// (the Stage-2 UI submits the entire set), so there is no per-module mutation surface.
/// </summary>
public interface ITenantNavPreferenceRepository
{
    Task<IReadOnlyList<TenantNavPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Atomically replaces the tenant's ENTIRE preference set with <paramref name="items"/>.</summary>
    Task ReplaceForTenantAsync(Guid tenantId, IReadOnlyCollection<TenantNavPreference> items, CancellationToken ct = default);
}
