using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record UpdateNotificationTemplateCommand(Guid Id, Guid? TenantId, NotificationTemplateUpsertRequest Request)
    : IRequest<Response<NotificationTemplateDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Update,
            "NotificationTemplate",
            Id,
            SourceModule: "MOD-0027",
            IsPlatformGlobal: Request.IsPlatformDefault,
            TargetTenantId: TenantId,
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.template.updated",
                ["TemplateKey"] = Request.TemplateKey,
                ["Locale"] = Request.Locale,
                ["Channel"] = Request.Channel
            });
}
