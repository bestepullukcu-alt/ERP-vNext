namespace Diten.DataKnowledgeService.Application.Contracts;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
