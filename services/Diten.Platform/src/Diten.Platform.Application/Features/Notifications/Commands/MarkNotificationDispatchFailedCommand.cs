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
    DateTimeOffset? NextRetryAt = null,
    /// <summary>BL-406 — true only when the CALLER (currently only <c>EmailDispatchJob</c>) has determined this
    /// failed attempt is the PERMANENT one: no further retry will ever become due for this dispatch. Never
    /// computed inside this command/handler — the handler has no way to know maxRetryCount on its own (it is not
    /// persisted on the dispatch), so it trusts the caller's own answer, the same way it already trusts the
    /// caller's RetryCount/NextRetryAt above. Defaults false so every existing caller (the admin
    /// NotificationsController retry-failed endpoint, and every pre-BL-406 test) is unaffected.</summary>
    bool IsPermanentFailure = false)
    : IRequest<Response<NotificationDispatchDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(AuditCategory.PlatformConfiguration, AuditOperation.Update, "NotificationDispatch", DispatchId, SourceModule: "MOD-0027", TargetTenantId: TenantId, Metadata: new Dictionary<string, object?> { ["EventName"] = "notifications.dispatch.failed", ["ErrorCode"] = ErrorCode });
}
