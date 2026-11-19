using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>
/// WMS draft §4 <c>bulkAssignPutawayTasks</c> (pack Q-13 A) — a supervisor assigns up to 100 tasks, each at its own version; one central record
/// for the command with no entity (pack §21, the bulk pattern of <c>BulkDeleteWarehouseLocationRoleCommand</c>).
/// </summary>
public sealed record BulkAssignPutawayTaskCommand(Guid AssigneeUserId, IReadOnlyList<BulkAssignItem> Items, string IdempotencyKey)
    : IRequest<Response<BulkAssignResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Assign", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => null;
}
