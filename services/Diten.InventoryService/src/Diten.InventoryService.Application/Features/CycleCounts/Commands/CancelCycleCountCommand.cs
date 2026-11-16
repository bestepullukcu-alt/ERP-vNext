using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>§4.1 <c>cancelCycleCount</c> — Draft: soft delete; Released / Counting: cancel state, claims released (pack §8.3, F-1).</summary>
public sealed record CancelCycleCountCommand(Guid CountId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("LifecycleTransition", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => CountId;
}
