using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>markPutawayConfirmationReturned</c> — "Geri taşındı": an unresolved Refused confirmation closes, no movement (F-4).</summary>
public sealed record MarkPutawayConfirmationReturnedCommand(Guid TaskId, int Seq, string? Reason, string IdempotencyKey, int ExpectedVersion, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("LifecycleTransition", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
