using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record DeletePlatformAdministratorCommand(Guid Id, PlatformAdministratorVersionRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.IdentityAccess, Operation: AuditOperation.Delete, EntityType: "PlatformAdministrator",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "platform-identity");
}
