using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>§4.1 <c>bulkDeleteCycleCounts</c> — soft delete of Draft counts with one reason; a row result each (draft §3.5).</summary>
public sealed record BulkDeleteCycleCountCommand(IReadOnlyList<CycleCountBulkItemInput> Items, string? Reason, string IdempotencyKey)
    : IRequest<Response<BulkDeleteCycleCountsResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Delete", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => null;
}
