using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Commands;

/// <summary>
/// The maintenance service's internal command (pack §8.17 step 6) — run per outbound in its own scope with the outbound's creator as actor:
/// an <c>Allocating</c> outbound whose recovery lock passed re-sends the SAME FU03 batch with the SAME keys from the persisted plan. Central
/// record (§21): 1 only when it writes <c>Allocated</c>; <c>AllocationFailed</c> → the command fails (0, <c>Abandoned</c>); nothing to do → no row.
/// </summary>
public sealed record CompleteOutboundAllocationCommand(Guid OutboundId)
    : IRequest<Response<bool>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}
