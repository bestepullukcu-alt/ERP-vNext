using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Loads.Commands;
public sealed record TransitionLoadCommand(Guid LoadId, JsonElement Body) : IRequest<Response<LoadResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.LoadModule, SupplyChainCentralAudit.Transition, "LoadPlan");
    // The repository enlists with the id of the aggregate it wrote; the command already names it.
    public Guid? CentralAuditEntityId(object? response) => LoadId;
}
