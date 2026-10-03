using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

// TenantId: the route's tenant (null = a platform default). See NotificationTemplateScope.
public sealed record ArchiveNotificationTemplateCommand(Guid Id, Guid? TenantId = null)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Delete,
            "NotificationTemplate",
            Id,
            SourceModule: "MOD-0027",
            // The record names the tenant whose template it was (a platform default is a platform-global record).
            IsPlatformGlobal: TenantId is null,
            TargetTenantId: TenantId,
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.template.archived"
            });
}
