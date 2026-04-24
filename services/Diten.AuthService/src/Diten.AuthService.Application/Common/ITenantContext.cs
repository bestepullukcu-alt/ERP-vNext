namespace Diten.AuthService.Application.Common;

/// <summary>
/// Tüm katmanlarda geçerli tenant ID'sine erişim sağlar.
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
