using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>cancelPutawayTask</c> — Cancelled while no confirmation is Posted (Posted movements are never undone here).</summary>
public sealed record CancelPutawayTaskCommand(Guid TaskId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("LifecycleTransition", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
