using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>confirmPutawayTask</c> (pack §8.3) — <c>Supervisor</c> = the caller holds <c>warehouse.putaway-tasks.update</c> (from the token, never the body).</summary>
public sealed record ConfirmPutawayTaskCommand(Guid TaskId, ConfirmPutawayTaskInput Confirmation, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
