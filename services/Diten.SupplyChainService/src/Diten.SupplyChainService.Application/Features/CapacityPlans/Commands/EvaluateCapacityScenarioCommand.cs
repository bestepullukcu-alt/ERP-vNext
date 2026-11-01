using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans.Commands;
public sealed record EvaluateCapacityScenarioCommand(Guid CapacityPlanId, Guid ScenarioId, EvaluateCapacityScenarioRequest Body) : IRequest<Response<CapacityEvaluationResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.CapacityModule, SupplyChainCentralAudit.Execute, "CapacityEvaluation");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
