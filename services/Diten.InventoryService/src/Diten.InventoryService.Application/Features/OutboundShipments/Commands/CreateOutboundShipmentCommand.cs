using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Commands;

/// <summary>
/// WMS draft §11 <c>createOutboundShipment</c> (pack §8.17, §8.24) — the outbound and its allocation together; the create key is stored on the
/// outbound, scoped to the creating user. Central record (§21): 1 only when the outbound becomes <c>Allocated</c>; a refused allocation after
/// transaction A leaves the row <c>Abandoned</c>; a process stopped after transaction A leaves it <c>Unconfirmed</c>.
/// </summary>
public sealed record CreateOutboundShipmentCommand(CreateOutboundShipmentInput Outbound, string IdempotencyKey)
    : IRequest<Response<OutboundShipmentViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Create", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<OutboundShipmentViewDto>(response)?.OutboundId);
}
