using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>retryPutawayConfirmation</c> (pack §8.3 "Kurtarma") — the same posting key, dates and serials; no attempt limit.</summary>
public sealed record RetryPutawayConfirmationCommand(Guid TaskId, int Seq, string IdempotencyKey, bool Supervisor)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
