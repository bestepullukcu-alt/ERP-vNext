using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>claimPutawayTask</c> — an operator assigns an unassigned task to himself or herself (M-5).</summary>
public sealed record ClaimPutawayTaskCommand(Guid TaskId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Assign", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
