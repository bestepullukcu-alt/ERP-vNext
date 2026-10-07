using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

public sealed record DeactivateItemCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Deactivate);
    public AuditDetails AuditDetails { get; } = new();
}
