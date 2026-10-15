using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 S2 (§8.1): Draft → PendingActivation, the QA Manager's approval (ITEM_ACTIVATION) started. §21:
// item.submitted — path b; the record carries the request (operation, kind) and where the item went.
public sealed record SubmitItemCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Update);
    public AuditDetails AuditDetails { get; } = new();
}
