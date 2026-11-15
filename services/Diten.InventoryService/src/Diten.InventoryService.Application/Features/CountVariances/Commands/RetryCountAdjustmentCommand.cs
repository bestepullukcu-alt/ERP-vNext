using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>retryCountAdjustment</c> (pack §8.6, §8.7; AC-25, AC-27) — an approved line's Pending (or reservation-refused) correction: the
/// ledger key first, then the same request under the same key. If-Match = the line's version.
/// </summary>
public sealed record RetryCountAdjustmentCommand(Guid LineId, string IdempotencyKey, int ExpectedVersion, bool ReadsVariances = false)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Execute", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
