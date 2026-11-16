using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>updatePutawayTask</c> (If-Match) — the source bucket never changes (§8.15).</summary>
public sealed record UpdatePutawayTaskCommand(Guid TaskId, UpdatePutawayTaskInput Change, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
