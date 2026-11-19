using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>requestCountRecount</c> (pack §8.5, §4.7; AC-32, AC-13 (e)) — ONE transaction: a new count of the selected keys only, released at
/// once with its own snapshot, the selected source lines Recounted and the bins' claims handed over. If-Match = the SOURCE count's version.
/// </summary>
public sealed record RequestCountRecountCommand(Guid CountId, CycleCountRecountInput Input, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Create", "CycleCount");

    public Guid? CentralAuditEntityId(object? response)
        => response is Response<CycleCountDto> { Data: { } created } && Guid.TryParse(created.CountId, out var id) ? id : CountId;
}
