using Diten.BuildingBlocks.CentralAudit;using Diten.SupplyChainService.Application.Common;
using System.Text.Json;using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
public sealed record RecordSandopSignOffCommand(SandopCommandContext Context,Guid? PlanId,JsonElement Body):IRequest<SandopResult>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.SandopModule, SupplyChainCentralAudit.Update, "SandopPlan");
    // The record is filed on the plan (the repository enlists the plan id); RequestType tells snapshot from sign-off.
    public Guid? CentralAuditEntityId(object? response) => PlanId;
}
