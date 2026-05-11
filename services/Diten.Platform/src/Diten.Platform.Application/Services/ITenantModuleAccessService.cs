using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Services;

public interface ITenantModuleAccessService
{
    Task<bool> HasAccessAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task<TenantModuleEffectiveAccess> GetEffectiveAccessAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task EnsureAccessOrThrowAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task<TenantModuleEffectiveAccessDto> GetEffectiveAccessDetailAsync(Guid tenantId, string moduleCode, CancellationToken ct = default);
}
