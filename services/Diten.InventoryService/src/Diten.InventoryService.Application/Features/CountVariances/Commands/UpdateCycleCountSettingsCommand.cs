using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>updateCycleCountSettings</c> — the ONE PUT (pack §4.4, §5; AC-49, AC-51): the company's tolerance, recount limit and line limit.
/// If-Match = the settings' version (<c>W/"0"</c> while the defaults hold).
/// </summary>
public sealed record UpdateCycleCountSettingsCommand(CycleCountSettingsInput Input, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountSettingsDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Update", "CycleCountSettings");

    public Guid? CentralAuditEntityId(object? response) => null;
}
