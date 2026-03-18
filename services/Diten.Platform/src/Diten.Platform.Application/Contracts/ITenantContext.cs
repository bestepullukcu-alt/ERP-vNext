namespace Diten.Platform.Application.Contracts;

public interface ITenantContext
{
    Guid TenantId { get; }

    bool IsResolved { get; }

    void SetTenant(Guid tenantId);
}
