using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Returns.Commands;
public sealed record TransitionReturnCommand(Guid ReturnId,JsonElement Body):IRequest<Response<ReturnResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ReturnModule, SupplyChainCentralAudit.Transition, "ReturnOrder");
    // The repository enlists with the id of the aggregate it wrote; the command already names it.
    public Guid? CentralAuditEntityId(object? response) => ReturnId;
}
