using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Commands;

/// <summary>
/// Mark ONE of the caller's own notifications read.
///
/// <para>Carries the notification id and nothing else. Whose notification it is comes from the token — see
/// <c>GetMyNotificationsQuery</c> for why no identity appears on any of these records.</para>
/// </summary>
public sealed record MarkMyNotificationReadCommand(Guid NotificationId)
    : IRequest<Response<UserNotificationReadResultDto>>;

/// <summary>Mark every unread notification of the caller read. No parameters — there is nothing to choose.</summary>
public sealed record MarkAllMyNotificationsReadCommand
    : IRequest<Response<UserNotificationReadResultDto>>;
