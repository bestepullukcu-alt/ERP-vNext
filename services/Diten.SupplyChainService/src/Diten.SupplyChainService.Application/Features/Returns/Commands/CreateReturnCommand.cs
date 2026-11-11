using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Returns.Commands;
public sealed record CreateReturnCommand(JsonElement Body):IRequest<Response<ReturnResponse>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ReturnModule, SupplyChainCentralAudit.Create, "ReturnOrder");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
