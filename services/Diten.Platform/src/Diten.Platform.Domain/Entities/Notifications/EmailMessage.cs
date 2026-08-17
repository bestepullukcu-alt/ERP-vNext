namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class EmailMessage
{
    public IReadOnlyList<EmailRecipient> To { get; set; } = [];
    public IReadOnlyList<EmailRecipient> Cc { get; set; } = [];
    public IReadOnlyList<EmailRecipient> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? BodyHtmlPreview { get; set; }
    public string? BodyTextPreview { get; set; }
}
