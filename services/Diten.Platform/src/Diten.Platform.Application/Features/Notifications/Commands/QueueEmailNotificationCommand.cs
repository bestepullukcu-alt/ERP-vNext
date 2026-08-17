using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record QueueEmailNotificationCommand(Guid TenantId, QueueEmailNotificationRequest Request, string? CorrelationId = null)
    : IRequest<Response<NotificationDispatchDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        new(
            AuditCategory.PlatformConfiguration,
            AuditOperation.Create,
            "NotificationDispatch",
            SourceModule: "MOD-0027",
            TargetTenantId: TenantId,
            Metadata: new Dictionary<string, object?>
            {
                ["EventName"] = "notifications.email.queued",
                ["TemplateKey"] = Request.TemplateKey,
                ["Locale"] = Request.Locale,
                ["RecipientCount"] = Request.To.Count + (Request.Cc?.Count ?? 0) + (Request.Bcc?.Count ?? 0)
            });
}
