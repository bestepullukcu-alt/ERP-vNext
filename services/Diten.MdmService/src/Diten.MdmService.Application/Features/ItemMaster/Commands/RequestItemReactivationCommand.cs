using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 S2 (§8.1, CT decision 4): a reactivation is an activation — Deactivated → PendingActivation, the same
// approval (ActivationKind = Reactivation). §21: item.reactivation-submitted — path b.
public sealed record RequestItemReactivationCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Reactivate);
    public AuditDetails AuditDetails { get; } = new();
}
