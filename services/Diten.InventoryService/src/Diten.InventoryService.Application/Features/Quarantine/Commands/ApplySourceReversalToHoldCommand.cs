using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Quarantine.Commands;

/// <summary>
/// MOD-0175-FU01 (pack §3, §8.2) — internal, no HTTP route: the source movement of a hold was reversed by
/// <paramref name="ReversalTransactionId"/>; apply the §8.3 rule table (Voided / Conflict / NoChange). Sent by the post-commit observer,
/// the sweeper and the apply engine (W5 / W6); its central record is filed under the System actor (Q-03 = A) whoever sends it.
/// </summary>
public sealed record ApplySourceReversalToHoldCommand(string ReversalTransactionId)
    : IRequest<Response<SourceReversalResultDto>>, ICentralAuditDeclaration, IQuarantineSystemActorCommand
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "QuarantineHold");
    public Guid? CentralAuditEntityId(object? response) => Data<SourceReversalResultDto>(response)?.RecordId;
}
