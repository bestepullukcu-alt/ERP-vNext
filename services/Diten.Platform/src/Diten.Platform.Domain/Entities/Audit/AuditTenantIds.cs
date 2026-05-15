namespace Diten.Platform.Domain.Entities.Audit;

public static class AuditTenantIds
{
    public static readonly Guid PlatformSystemTenantId = Guid.Parse("00000000-0000-0000-0000-000000002121");

    public static bool IsPlatformSystemTenantId(Guid tenantId)
    {
        return tenantId == PlatformSystemTenantId;
    }
}
