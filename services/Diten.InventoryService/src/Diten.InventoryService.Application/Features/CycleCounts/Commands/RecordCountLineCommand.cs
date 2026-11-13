using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>recordCycleCountLine</c> (pack §4.2, §8.3, §12; AC-14) — the counted quantity of one line; <c>If-Match</c> is the LINE's
/// version (re-entry until submit). The first entry takes the count to <c>Counting</c>.
/// </summary>
public sealed record RecordCountLineCommand(Guid CountId, Guid LineId, CycleCountEntryInput Entry, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Update", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
