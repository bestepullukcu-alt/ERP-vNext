using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// S1 skeleton: the activation approval is S2. The state is checked; nothing is written.
public sealed record SubmitItemCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Update);
}
