using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantMessagingSettingsRepository
{
    Task<TenantMessagingSettings> CreateAsync(TenantMessagingSettings settings, CancellationToken ct = default);
    Task<TenantMessagingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantMessagingSettings?> GetPlatformDefaultAsync(CancellationToken ct = default);
    Task<TenantMessagingSettings?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMessagingSettings>> ListTenantSettingsAsync(int skip = 0, int take = 50, CancellationToken ct = default);
    Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default);
    Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default);
}
