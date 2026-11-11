using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>WMS-COUNT-BUNDLE §4.1 <c>createCycleCount</c> (pack §4.1, §8.12, §12) — the create key is stored on the count, scoped to the creating user.</summary>
public sealed record CreateCycleCountCommand(CycleCountFormInput Count, string IdempotencyKey)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Create", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<CycleCountDto>(response)?.CountId);
}
