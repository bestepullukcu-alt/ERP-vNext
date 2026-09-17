namespace Diten.HumanCapitalService.Application.Contracts;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
