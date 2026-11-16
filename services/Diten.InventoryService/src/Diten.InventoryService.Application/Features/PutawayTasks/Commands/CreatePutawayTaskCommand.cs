using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>createPutawayTask</c> (pack §8.2, §12) — the create key is stored on the task, scoped to the creating user.</summary>
public sealed record CreatePutawayTaskCommand(CreatePutawayTaskInput Task, string IdempotencyKey)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Create", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<WarehouseTaskDto>(response)?.TaskId);
}
