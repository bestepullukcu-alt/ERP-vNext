using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>WMS-COUNT-BUNDLE <c>rejectCountVariance</c> (pack §8.5; AC-31) — a reason (1..500) is required; the line is Rejected, nothing is posted.</summary>
public sealed record RejectCountVarianceCommand(Guid LineId, string? Reason, string IdempotencyKey, int ExpectedVersion, bool ReadsVariances = false)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("LifecycleTransition", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
