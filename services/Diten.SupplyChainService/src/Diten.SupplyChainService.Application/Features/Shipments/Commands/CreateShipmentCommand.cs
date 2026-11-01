using Diten.BuildingBlocks.CentralAudit;
using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Commands;
public sealed record CreateShipmentCommand(ShipmentModels.Create Body) : IRequest<Response<JsonObject>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplyChainCentralAudit.For(SupplyChainCentralAudit.ShipmentModule, SupplyChainCentralAudit.Create, "Shipment");
    // The repository enlists with the id of the aggregate it wrote.
    public Guid? CentralAuditEntityId(object? response) => null;
}
