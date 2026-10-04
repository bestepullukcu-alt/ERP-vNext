using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

/// <param name="DegradedReason">BL-454 — set by a retry that could only send the stored preview (why: VariablesRedacted,
/// TemplateVersionChanged …). Recorded on the dispatch's existing error fields so the monitoring screen shows it.</param>
public sealed record MarkNotificationDispatchSentCommand(Guid TenantId, Guid DispatchId, string? ProviderMessageId, string? DegradedReason = null)
    : IRequest<Response<NotificationDispatchDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(AuditCategory.PlatformConfiguration, AuditOperation.Update, "NotificationDispatch", DispatchId, SourceModule: "MOD-0027", TargetTenantId: TenantId, Metadata: new Dictionary<string, object?> { ["EventName"] = "notifications.dispatch.sent" });
}
