using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// Response<Guid>: the new item's id is the audit record's EntityId.
public sealed record CreateItemCommand(ItemModels.CreateItemRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Create);
}
