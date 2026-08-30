namespace Diten.Platform.Domain.Enums;

public enum MessagingProviderCode
{
    Fake = 0,
    Smtp = 1,
    SendGrid = 2
}

/// <summary>
/// How a notification reaches a person.
///
/// <para>Values are persisted — append only, never renumber. <c>Email</c> stays 0 so every stored dispatch,
/// template and event definition keeps meaning exactly what it meant before <c>InApp</c> existed.</para>
///
/// <para><b>InApp is not a second kind of e-mail.</b> The e-mail channel is MESSAGE-shaped: one
/// <see cref="Entities.Notifications.NotificationDispatch"/> row per message, addressed to a list of e-mail
/// addresses. The in-app channel is PERSON-shaped: one
/// <see cref="Entities.Notifications.UserNotification"/> row per reader, keyed on their user id, with its own
/// read state. They are two records because they answer two different questions — "what did we send?" and
/// "what have I not read yet?" — and collapsing them would break the platform dispatch-monitoring screen that
/// reads the first.</para>
/// </summary>
public enum NotificationChannelCode
{
    Email = 0,
    InApp = 1
}

/// <summary>
/// How loudly an in-app notification should present itself. Persisted — append only, never renumber.
///
/// <para>NOT annotated with <c>JsonStringEnumConverter</c>, and that is the convention rather than an
/// omission: this enum never appears in a request or response body. <c>UserNotificationDto</c> projects it as
/// a plain <c>string</c>, exactly as the notification DTOs already project Channel, Status and ProviderCode.
/// The attribute belongs on enums that DO cross the wire — see the header of <c>Enums/Tasks/TaskEnums.cs</c>,
/// where its absence meant System.Text.Json accepted integers only.</para>
/// </summary>
public enum UserNotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Critical = 3
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
