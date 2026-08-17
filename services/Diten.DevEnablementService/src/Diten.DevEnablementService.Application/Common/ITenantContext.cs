namespace Diten.DevEnablementService.Application.Common;

/// <summary>
/// Scoped tenant bilgisi — TenantResolutionMiddleware tarafından populate edilir.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
    bool IsPlatformContext { get; }
    Guid? TargetTenantId { get; }

    void SetTenant(Guid tenantId);
    void SetPlatformContext(Guid targetTenantId);
}
