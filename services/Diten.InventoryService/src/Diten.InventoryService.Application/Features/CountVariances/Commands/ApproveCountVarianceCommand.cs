using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// WMS-COUNT-BUNDLE <c>approveCountVariance</c> (pack §8.5, §8.6; AC-22, AC-23, AC-24, AC-48, AC-52) — four eyes, then transaction 1 (Approved +
/// the Pending correction), the ONE posting, transaction 2 (Posted / Refused / IntegrityError). If-Match = the LINE's version.
/// </summary>
public sealed record ApproveCountVarianceCommand(Guid LineId, string IdempotencyKey, int ExpectedVersion, bool ReadsVariances = false)
    : IRequest<Response<CycleCountLineDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("LifecycleTransition", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
