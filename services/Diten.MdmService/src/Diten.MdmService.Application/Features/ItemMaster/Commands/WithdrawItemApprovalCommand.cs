using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 S2 (§8.1): PendingActivation → Draft (Deactivated for a reactivation) — the maker takes the request
// back; the approval is cancelled in the engine first. §21: item.approval-withdrawn — path b.
public sealed record WithdrawItemApprovalCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Update);
    public AuditDetails AuditDetails { get; } = new();
}
