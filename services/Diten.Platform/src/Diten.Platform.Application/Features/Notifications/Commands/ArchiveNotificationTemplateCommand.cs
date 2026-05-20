using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record ArchiveNotificationTemplateCommand(Guid Id)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Delete,
            "NotificationTemplate",
            Id,
            SourceModule: "MOD-0027",
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.template.archived"
            });
}
