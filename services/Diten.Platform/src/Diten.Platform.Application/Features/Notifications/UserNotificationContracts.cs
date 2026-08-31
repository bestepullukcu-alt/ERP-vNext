using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications;

/// <summary>
/// BL-025 — one in-app notification as the reader's own client sees it.
///
/// <para><b>There is no UserId on this DTO, and that is deliberate.</b> Every row in a response is the
/// caller's own by construction — the scope came from the token, not from the request — so a user id here
/// would carry no information and would invite a client to start filtering on it, which is the habit that
/// turns "the server decides whose data this is" into "the client asks nicely".</para>
///
/// <para><c>Severity</c> is projected as a string like every other enum in this feature's DTOs, so the wire
/// stays readable and no caller has to know that 0 means Info.</para>
/// </summary>
public sealed record UserNotificationDto(
    Guid Id,
    string EventCode,
    string Title,
    string? Body,
    string? TargetUrl,
    string Severity,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    /// <summary>Unread is the absence of a read timestamp — one field, so the two cannot disagree.</summary>
    public bool IsRead => ReadAt is not null;
}

/// <summary>
/// One page of the caller's notifications, plus the unread total.
///
/// <para><b>Why the count travels with the page.</b> A bell badge needs "how many unread", and the only other
/// way to get it is to count the unread rows in the page — a number that silently means "unread among the
/// first 20". Shipping that is how the bell ends up lying again, which is exactly the defect the theme's
/// hard-coded 8 was stripped for.</para>
/// </summary>
public sealed record UserNotificationPageDto(
    IReadOnlyList<UserNotificationDto> Items,
    long UnreadCount,
    int Page,
    int PageSize);

/// <summary>What a mark-read call changed. A count, so "nothing to do" is distinguishable from "done".</summary>
public sealed record UserNotificationReadResultDto(long MarkedCount);

public static class UserNotificationMappings
{
    public static UserNotificationDto ToDto(this UserNotification notification) =>
        new(
            notification.Id,
            notification.EventCode,
            notification.Title,
            notification.Body,
            notification.TargetUrl,
            notification.Severity.ToString(),
            notification.CreatedAt,
            notification.ReadAt);
}
