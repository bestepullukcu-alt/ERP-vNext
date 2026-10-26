using Diten.BuildingBlocks.CentralAudit;using Diten.SupplyChainService.Application.Common;
using System.Text.Json;using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
public sealed record CreateSandopPlanCommand(SandopCommandContext Context,Guid? PlanId,JsonElement Body):IRequest<SandopResult>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.SandopModule, SupplyChainCentralAudit.Create, "SandopPlan");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
