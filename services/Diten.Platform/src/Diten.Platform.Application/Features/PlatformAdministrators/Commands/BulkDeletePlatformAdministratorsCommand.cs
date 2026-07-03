using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record BulkDeletePlatformAdministratorsCommand(IReadOnlyList<PlatformAdministratorBulkDeleteItemRequest> Items)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.IdentityAccess, Operation: AuditOperation.Delete, EntityType: "PlatformAdministrator",
        IsPlatformGlobal: true, SourceModule: "platform-identity",
        Metadata: new Dictionary<string, object?> { ["count"] = Items.Count });
}
