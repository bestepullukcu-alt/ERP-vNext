using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class NotificationDispatch : BaseEntity
{
    public Guid TenantId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string Locale { get; set; } = "en";
    public NotificationChannelCode Channel { get; set; } = NotificationChannelCode.Email;
    public MessagingProviderCode ProviderCode { get; set; } = MessagingProviderCode.Fake;
    public string? ProviderMessageId { get; set; }
    public NotificationDispatchStatus Status { get; set; } = NotificationDispatchStatus.Queued;
    public List<EmailRecipient> To { get; set; } = [];
    public List<EmailRecipient> Cc { get; set; } = [];
    public List<EmailRecipient> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? BodyHtmlPreview { get; set; }
    public string? BodyTextPreview { get; set; }
    public string VariablesJson { get; set; } = "{}";
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }

    public bool TryMarkSent(string? providerMessageId, DateTimeOffset now)
    {
        if (Status != NotificationDispatchStatus.Queued)
        {
            return false;
        }

        Status = NotificationDispatchStatus.Sent;
        ProviderMessageId = providerMessageId;
        SentAt = now;
        UpdatedAt = now;
        Version++;
        return true;
    }

    public bool TryMarkFailed(string errorCode, string errorMessage, DateTimeOffset now)
    {
        if (Status is NotificationDispatchStatus.Sent or NotificationDispatchStatus.Cancelled)
        {
            return false;
        }

        Status = NotificationDispatchStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        FailedAt = now;
        UpdatedAt = now;
        Version++;
        return true;
    }

    public bool TryCancel(DateTimeOffset now)
    {
        if (Status != NotificationDispatchStatus.Queued)
        {
            return false;
        }

        Status = NotificationDispatchStatus.Cancelled;
        UpdatedAt = now;
        Version++;
        return true;
    }
}
