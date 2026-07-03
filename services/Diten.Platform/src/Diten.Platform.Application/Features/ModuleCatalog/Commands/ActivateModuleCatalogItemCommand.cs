using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record ActivateModuleCatalogItemCommand(Guid Id)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.ModuleCatalog, Operation: AuditOperation.Activate, EntityType: "ModuleCatalogItem",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "module-catalog");
}
