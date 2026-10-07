using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// FIX1 M7: Platform's AuditOperation has no Archive (measured: … Execute = 15, LifecycleTransition = 16) — Deactivate,
// with Metadata.lifecycle = "Retired".
public sealed record RetireItemCommand(Guid ItemId, ItemModels.ItemLifecycleRequest Request)
    : IRequest<Response<ItemModels.ItemDto>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Deactivate);
    public AuditDetails AuditDetails { get; } = new();
}
