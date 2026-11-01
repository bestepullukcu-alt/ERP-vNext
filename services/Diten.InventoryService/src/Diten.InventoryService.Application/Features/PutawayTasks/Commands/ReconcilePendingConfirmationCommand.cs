using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>The reconciler's internal command (pack §8.3 "Sweeper") — ledger key lookups only; sent only when there is something to write.</summary>
public sealed record ReconcilePendingConfirmationCommand(Guid TaskId, bool DefensiveBranch)
    : IRequest<Response<bool>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
