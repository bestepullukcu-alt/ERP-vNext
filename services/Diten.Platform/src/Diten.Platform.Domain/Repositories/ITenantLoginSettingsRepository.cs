using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantLoginSettingsRepository
{
    Task<TenantLoginSettings> CreateAsync(TenantLoginSettings settings, CancellationToken ct = default);
    Task<TenantLoginSettings?> GetByTenantRefIdAsync(Guid tenantRefId, CancellationToken ct = default);
    Task UpdateAsync(TenantLoginSettings settings, CancellationToken ct = default);
}
