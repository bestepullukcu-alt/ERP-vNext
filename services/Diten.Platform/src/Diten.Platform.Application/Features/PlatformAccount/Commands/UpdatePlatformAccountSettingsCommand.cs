using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAccount.Commands;

public sealed record UpdatePlatformAccountSettingsCommand(UpdatePlatformAccountSettingsRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.PlatformConfiguration, Operation: AuditOperation.Update, EntityType: "PlatformAccountSettings",
        SourceModule: "platform-account", IsPlatformGlobal: true);
}
