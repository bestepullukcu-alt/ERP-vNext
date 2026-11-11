using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Commands;
public sealed record TransitionShipmentCommand(Guid ShipmentId, ShipmentModels.Transition Body) : IRequest<Response<JsonObject>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ShipmentModule, SupplyChainCentralAudit.Transition, "Shipment");
    // The repository enlists with the id of the aggregate it wrote; the command already names it.
    public Guid? CentralAuditEntityId(object? response) => ShipmentId;
}
