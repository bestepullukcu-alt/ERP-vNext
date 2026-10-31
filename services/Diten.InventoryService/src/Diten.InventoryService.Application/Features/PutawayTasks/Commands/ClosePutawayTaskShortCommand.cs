using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>closePutawayTaskShort</c> — Completed with at least one Posted confirmation and a reason (never for a serial SKU).</summary>
public sealed record ClosePutawayTaskShortCommand(Guid TaskId, string? Reason, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("LifecycleTransition", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
