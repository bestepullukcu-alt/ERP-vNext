namespace Diten.TalentEcosystemService.Application.Contracts;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
