using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Quarantine.Commands;

/// <summary>MOD-0175-FU01 — <see cref="ApplySourceReversalToHoldCommand"/> for a blocked-stock record (origin <c>OtherInbound</c> / <c>ManualBlock</c>).</summary>
public sealed record ApplySourceReversalToBlockedCommand(string ReversalTransactionId)
    : IRequest<Response<SourceReversalResultDto>>, ICentralAuditDeclaration, IQuarantineSystemActorCommand
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "BlockedStock");
    public Guid? CentralAuditEntityId(object? response) => Data<SourceReversalResultDto>(response)?.RecordId;
}
