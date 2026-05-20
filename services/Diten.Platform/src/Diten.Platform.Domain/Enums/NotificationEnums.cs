namespace Diten.Platform.Domain.Enums;

public enum MessagingProviderCode
{
    Fake = 0,
    Smtp = 1,
    SendGrid = 2
}

public enum NotificationChannelCode
{
    Email = 0
}

public enum NotificationTemplateStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

public enum NotificationDispatchStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    Cancelled = 3
}

public enum TemplateVariableType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Date = 3,
    Url = 4
}

public enum NotificationFallbackPolicy
{
    UsePlatformDefault = 0,
    DisableSending = 1,
    FailFast = 2
}
