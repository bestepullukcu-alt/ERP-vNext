using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
public sealed record CreateCapacityScenarioCommand(Guid CapacityPlanId, CreateCapacityScenarioRequest Body) : IRequest<Response<CapacityScenarioResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.CapacityModule, SupplyChainCentralAudit.Create, "CapacityScenario");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
