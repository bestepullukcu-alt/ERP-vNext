using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Commands;

/// <summary>WMS draft §4 <c>resolvePutawayIntegrity</c> — "Uzlaştır", the only exit of an IntegrityError confirmation (inspection 03 F-2, 04 F-1).</summary>
public sealed record ResolvePutawayIntegrityCommand(Guid TaskId, int Seq, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseTaskDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("LifecycleTransition", "WarehouseTask");
    public Guid? CentralAuditEntityId(object? response) => TaskId;
}
