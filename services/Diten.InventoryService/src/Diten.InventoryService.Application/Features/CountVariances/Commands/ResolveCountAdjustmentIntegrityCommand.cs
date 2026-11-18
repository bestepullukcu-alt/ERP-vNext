using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>resolveCountAdjustmentIntegrity</c> (pack §8.3 F-12; AC-26) — <c>AcceptHolder</c> (only when the holding movement IS this
/// line's correction and is not reversed) or <c>Reject</c>, with a reason. If-Match = the line's version.
/// </summary>
public sealed record ResolveCountAdjustmentIntegrityCommand(Guid LineId, string? Decision, string? Reason, string IdempotencyKey, int ExpectedVersion, bool ReadsVariances = false)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Execute", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
