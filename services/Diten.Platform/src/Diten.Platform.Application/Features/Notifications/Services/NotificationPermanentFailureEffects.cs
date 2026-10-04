using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// BL-406 / BL-454 — what happens ONCE when a dispatch's failure becomes permanent, whichever path decided it: the last
/// failed retry (<c>MarkNotificationDispatchFailedHandler</c>), the sweep's retry window, or a first send on a server
/// where no retry can ever run (<c>QueueEmailNotificationHandler</c>). One implementation, so none of the three can skip
/// the ops counter or a meeting organizer's notification.
/// </summary>
public sealed class NotificationPermanentFailureEffects
{
    // Ops-only counter. Meeting-related permanent failures ALSO get the organizer notification + attendee badge;
    // every producer (meeting or not) gets this. Static/self-registered — no DI needed.
    private static readonly Counter PermanentlyFailedCounter = Metrics.CreateCounter(
        "notification_dispatch_permanently_failed",
        "Notification dispatches whose final retry attempt was exhausted with no further retry due.",
        new CounterConfiguration { LabelNames = new[] { "template_key", "is_meeting_related" } });

    private const string MeetingTemplateKeyPrefix = "platform.meetings.";
    private const string MeetingUndeliveredEventCode = "platform.meetings.invite-undelivered";

    private readonly ILogger? _logger;
    private readonly IMeetingRepository? _meetings;
    private readonly IMeetingAttendeeRepository? _meetingAttendees;
    private readonly IUserNotificationRepository? _userNotifications;

    public NotificationPermanentFailureEffects(
        ILogger? logger,
        IMeetingRepository? meetings,
        IMeetingAttendeeRepository? meetingAttendees,
        IUserNotificationRepository? userNotifications)
    {
        _logger = logger;
        _meetings = meetings;
        _meetingAttendees = meetingAttendees;
        _userNotifications = userNotifications;
    }

    /// <summary>
    /// BL-406 — everything that happens ONCE, at the moment a dispatch's failure becomes permanent. Ops
    /// log line + counter fire for EVERY permanently-failed dispatch (rule 3); the organizer notification + the
    /// attendee "mail undelivered" badge fire ONLY for meeting-related mail (rule 1/2). Never allowed to fail the
    /// primary transition above — this runs strictly after that transition and its own event has already
    /// published.
    /// </summary>
    public async Task ApplyAsync(NotificationDispatch dispatch, bool silent, CancellationToken ct)
    {
        var isMeetingRelated = dispatch.TemplateKey.StartsWith(MeetingTemplateKeyPrefix, StringComparison.OrdinalIgnoreCase);

        PermanentlyFailedCounter.WithLabels(dispatch.TemplateKey, isMeetingRelated ? "true" : "false").Inc();
        _logger?.LogWarning(
            "email.dispatch.permanently_failed DispatchId={DispatchId} TenantId={TenantId} TemplateKey={TemplateKey} "
            + "IsMeetingRelated={IsMeetingRelated} RetryCount={RetryCount} ErrorCode={ErrorCode} Silent={Silent} CorrelationId={CorrelationId}",
            dispatch.Id, dispatch.TenantId, dispatch.TemplateKey, isMeetingRelated, dispatch.RetryCount, dispatch.ErrorCode, silent, dispatch.CorrelationId);

        // BL-454 — a SILENT close (a row far older than the retry window, closed the first time the jobs run) counts and
        // logs, but tells no organizer and badges no attendee about mail that died long ago.
        if (!isMeetingRelated || silent)
        {
            return;
        }

        if (dispatch.CausationId is not { } meetingId || dispatch.MeetingAttendeeUserId is not { } attendeeUserId)
        {
            // Meeting-templated mail with no (meetingId, attendeeUserId) attribution — cannot happen from
            // MeetingInviteMailer's own 1:1 dispatch path, but a future producer reusing the same template key
            // without setting both fields must not throw here; the ops log/counter above already fired.
            _logger?.LogWarning(
                "email.dispatch.permanently_failed.meeting_attribution_missing DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
            return;
        }

        // K12's own posture, one level down: an organizer notification failing to write must never surface as
        // this command failing (the dispatch's own Failed transition has already been committed and published
        // above).
        try
        {
            if (_meetingAttendees is not null)
            {
                await _meetingAttendees.MarkMailUndeliveredAsync(meetingId, attendeeUserId, DateTimeOffset.UtcNow, ct);
            }

            if (_meetings is null || _userNotifications is null)
            {
                return;
            }

            var meeting = await _meetings.GetByIdAsync(meetingId, ct);
            if (meeting is null)
            {
                return;
            }

            // The dispatch is 1:1 (BL-406's own MeetingInviteMailer change) — its single `To` entry IS the
            // attendee this failure is about. DisplayName falls back to the email so the organizer's notification
            // never reads as blank.
            var recipient = dispatch.To.Count > 0 ? dispatch.To[0] : null;
            var personLabel = recipient?.DisplayName ?? recipient?.Email ?? attendeeUserId.ToString();

            await _userNotifications.CreateAsync(
                new UserNotification
                {
                    TenantId = dispatch.TenantId,
                    UserId = meeting.OrganizerUserId,
                    EventCode = MeetingUndeliveredEventCode,
                    // Data the tenant typed (the meeting's own title) — no sentence composed here; a surface
                    // resolves its label from EventCode + these two pieces of data, same posture as
                    // TaskNotificationService.WriteInAppNotificationsAsync's own doc comment.
                    Title = meeting.Title,
                    Body = personLabel,
                    TargetUrl = $"/Meetings/{meeting.Id}",
                    Severity = UserNotificationSeverity.Warning
                },
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "email.dispatch.permanently_failed.organizer_notification_write_failed DispatchId={DispatchId} MeetingId={MeetingId}",
                dispatch.Id, meetingId);
        }
    }
}
