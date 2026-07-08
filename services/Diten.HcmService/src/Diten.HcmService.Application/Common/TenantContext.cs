namespace Diten.HcmService.Application.Common;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
    }

    public void Clear()
    {
        TenantId = null;
    }
}
