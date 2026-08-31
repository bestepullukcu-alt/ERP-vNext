using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record DeactivateModuleCatalogItemCommand(Guid Id)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider, ITransactionOwnedAuditCommand
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.ModuleCatalog, Operation: AuditOperation.Deactivate, EntityType: "ModuleCatalogItem",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "module-catalog");
}
