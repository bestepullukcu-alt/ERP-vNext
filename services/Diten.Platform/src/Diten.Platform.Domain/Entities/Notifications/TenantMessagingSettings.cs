using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class TenantMessagingSettings : BaseEntity
{
    public Guid? TenantId { get; set; }
    public bool IsPlatformDefault { get; set; }
    public MessagingProviderCode ProviderCode { get; set; } = MessagingProviderCode.Fake;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string? ReplyToEmail { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? ApiBaseUrl { get; set; }
    public string? CredentialSecretRef { get; set; }
    public bool IsEnabled { get; set; } = true;
    public NotificationFallbackPolicy FallbackPolicy { get; set; } = NotificationFallbackPolicy.UsePlatformDefault;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? ValidationStatus { get; set; }
    public string? ValidationError { get; set; }
}
