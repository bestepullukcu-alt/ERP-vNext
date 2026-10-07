using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// S1 skeleton: a reactivation is an activation (approved, S2). The state is checked; nothing is written.
public sealed record RequestItemReactivationCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Reactivate);
    public AuditDetails AuditDetails { get; } = new();
}
