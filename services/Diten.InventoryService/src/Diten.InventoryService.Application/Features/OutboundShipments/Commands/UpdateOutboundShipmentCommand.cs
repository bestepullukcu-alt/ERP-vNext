using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Commands;

/// <summary>WMS draft §11 <c>updateOutboundShipment</c> — header only (order ref, ship-to, note), If-Match; the allocation never changes.</summary>
public sealed record UpdateOutboundShipmentCommand(Guid OutboundId, UpdateOutboundShipmentInput Change, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<OutboundShipmentViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}
