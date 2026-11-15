using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>submitCycleCount</c> (pack §8.3, §8.4; AC-17…AC-20, AC-52 (b) (c)) — every Snapshot and Recount line counted, then each
/// line's variance from ONE snapshot read of the ledger; a count whose every line is final closes in the same transaction.
/// </summary>
public sealed record SubmitCycleCountCommand(Guid CountId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("LifecycleTransition", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => CountId;
}
