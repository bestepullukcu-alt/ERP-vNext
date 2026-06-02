namespace Diten.MdmService.Application.Common;

public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
    void SetTenant(Guid tenantId);
}
