using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

public sealed record MarkNotificationDispatchFailedCommand(
    Guid TenantId,
    Guid DispatchId,
    string ErrorCode,
    string ErrorMessage,
    int? RetryCount = null,
    DateTimeOffset? NextRetryAt = null)
    : IRequest<Response<NotificationDispatchDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(AuditCategory.PlatformConfiguration, AuditOperation.Update, "NotificationDispatch", DispatchId, SourceModule: "MOD-0027", TargetTenantId: TenantId, Metadata: new Dictionary<string, object?> { ["EventName"] = "notifications.dispatch.failed", ["ErrorCode"] = ErrorCode });
}
