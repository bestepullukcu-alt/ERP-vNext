using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record BulkDeleteModuleCatalogItemsCommand(IReadOnlyList<Guid> Ids)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider, ITransactionOwnedAuditCommand
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.ModuleCatalog, Operation: AuditOperation.Delete, EntityType: "ModuleCatalogItem",
        IsPlatformGlobal: true, SourceModule: "module-catalog",
        Metadata: new Dictionary<string, object?> { ["count"] = Ids.Count });
}
