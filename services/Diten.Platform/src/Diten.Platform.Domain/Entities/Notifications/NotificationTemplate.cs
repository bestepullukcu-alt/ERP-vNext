using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class NotificationTemplate : BaseEntity
{
    public Guid? TenantId { get; set; }
    public bool IsPlatformDefault { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public NotificationChannelCode Channel { get; set; } = NotificationChannelCode.Email;
    public string Locale { get; set; } = "en";
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyHtmlTemplate { get; set; } = string.Empty;
    public string? BodyTextTemplate { get; set; }
    public List<TemplateVariableDefinition> Variables { get; set; } = [];
    public NotificationTemplateStatus Status { get; set; } = NotificationTemplateStatus.Draft;
    public string? SemanticVersion { get; set; }
}
