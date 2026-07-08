namespace Diten.HcmService.Application.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }
}
