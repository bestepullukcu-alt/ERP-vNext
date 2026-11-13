using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>addCycleCountLine</c> (pack §4.2, §8.9, §12; AC-16) — stock found on a scope bin that the snapshot has no line for:
/// <c>Origin = Found</c>, expected 0, born counted.
/// </summary>
public sealed record AddCountLineCommand(Guid CountId, CycleCountFoundLineInput Line, string IdempotencyKey)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Create", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<CycleCountLineDto>(response)?.LineId);
}
