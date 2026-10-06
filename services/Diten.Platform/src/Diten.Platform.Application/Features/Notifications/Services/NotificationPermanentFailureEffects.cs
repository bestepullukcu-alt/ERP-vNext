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

    /// <summary>The claim's <c>UpdatedBy</c>: this prefix, the attempt number, the claiming host.</summary>
    public const string ClaimActorPrefix = "permanent-failure-effects#";

    /// <summary>After this many failed attempts on one row the effects are given up — named, counted, logged.</summary>
    public const int MaxAttempts = 5;

    private static readonly Counter EffectsGivenUpCounter = Metrics.CreateCounter(
        "notification_dispatch_permanent_effects_given_up",
        "Permanent-failure effects (organizer notification, attendee badge) that kept failing and were given up.",
        new CounterConfiguration { LabelNames = new[] { "template_key" } });

    /// <summary>
    /// BL-454 — the effects of a PENDING row, exactly once:
    /// <list type="number">
    ///   <item>CLAIM: a write conditional on the version read stamps <c>UpdatedAt</c> (when) and <c>UpdatedBy</c> (who,
    ///     attempt number) and raises <c>Version</c>. A second run that read the same version loses here and does nothing.</item>
    ///   <item>RUN the effects. If they fail, the marker stays pending — the row is re-driven by the sweep once it has been
    ///     idle for the grace period; after <see cref="MaxAttempts"/> failed attempts the effects are given up, named and counted.</item>
    ///   <item>MARK: the real time, conditional on the claimed version.</item>
    /// </list>
    /// A claim that went stale (its run died) is re-claimed after the grace period; only then can an effect repeat.
    /// </summary>
    /// <returns>True when this call ran the effects.</returns>
    public async Task<bool> ApplyAndMarkAsync(
        NotificationDispatch dispatch, bool silent, INotificationDispatchRepository repository, CancellationToken ct)
    {
        if (!NotificationDispatch.IsPermanentFailurePending(dispatch) || dispatch.Status != NotificationDispatchStatus.Failed)
        {
            return false;
        }

        var attempt = NextAttempt(dispatch.UpdatedBy);
        var readVersion = dispatch.Version;
        dispatch.UpdatedAt = DateTimeOffset.UtcNow;
        dispatch.UpdatedBy = $"{ClaimActorPrefix}{attempt}:{Environment.MachineName}";
        dispatch.Version = readVersion + 1;
        if (!await repository.TryUpdateAsync(dispatch, readVersion, dispatch.Status, ct))
        {
            _logger?.LogInformation(
                "email.dispatch.permanently_failed.claim_lost DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
            return false;
        }

        try
        {
            await RunAsync(dispatch, silent, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (attempt < MaxAttempts)
            {
                // Not marked: still pending, re-driven after the grace period.
                _logger?.LogWarning(
                    ex,
                    "email.dispatch.permanently_failed.effects_failed DispatchId={DispatchId} TenantId={TenantId} Attempt={Attempt} MaxAttempts={MaxAttempts}",
                    dispatch.Id, dispatch.TenantId, attempt, MaxAttempts);
                return false;
            }

            EffectsGivenUpCounter.WithLabels(dispatch.TemplateKey).Inc();
            _logger?.LogError(
                ex,
                "email.dispatch.permanently_failed.effects_given_up DispatchId={DispatchId} TenantId={TenantId} Attempt={Attempt} Reason=EffectsKeptFailing",
                dispatch.Id, dispatch.TenantId, attempt);
        }

        var claimedVersion = dispatch.Version;
        dispatch.PermanentlyFailedNotifiedAt = DateTimeOffset.UtcNow;
        dispatch.Version = claimedVersion + 1;
        if (!await repository.TryUpdateAsync(dispatch, claimedVersion, dispatch.Status, ct))
        {
            _logger?.LogWarning(
                "email.dispatch.permanently_failed.mark_lost DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
        }

        return true;
    }

    private static int NextAttempt(string? updatedBy)
    {
        if (updatedBy is null || !updatedBy.StartsWith(ClaimActorPrefix, StringComparison.Ordinal))
        {
            return 1;
        }

        var rest = updatedBy[ClaimActorPrefix.Length..];
        var colon = rest.IndexOf(':');
        return int.TryParse(colon < 0 ? rest : rest[..colon], out var previous) ? previous + 1 : 1;
    }

    /// <summary>
    /// The effects where nothing can re-drive them (a first send on a server where no retry job runs): run once, and a
    /// failure is logged by name rather than thrown into the transition that already happened.
    /// </summary>
    public async Task ApplyAsync(NotificationDispatch dispatch, bool silent, CancellationToken ct)
    {
        try
        {
            await RunAsync(dispatch, silent, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "email.dispatch.permanently_failed.organizer_notification_write_failed DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
        }
    }

    /// <summary>
    /// BL-406 — everything that happens ONCE when a dispatch's failure becomes permanent: for meeting mail the attendee's
    /// "mail undelivered" badge and the organizer's notification (rule 1/2), then for EVERY permanent failure the ops log
    /// line and counter (rule 3) — last, so a run that fails half way and is re-driven never counts twice. Throws when a
    /// store write fails: the caller decides whether that is retried (pending) or logged (no re-drive possible).
    /// </summary>
    private async Task RunAsync(NotificationDispatch dispatch, bool silent, CancellationToken ct)
    {
        var isMeetingRelated = dispatch.TemplateKey.StartsWith(MeetingTemplateKeyPrefix, StringComparison.OrdinalIgnoreCase);

        // BL-454 — a SILENT close (a row far older than the retry window, closed the first time the jobs run) counts and
        // logs, but tells no organizer and badges no attendee about mail that died long ago.
        if (isMeetingRelated && !silent)
        {
            await NotifyMeetingAsync(dispatch, ct);
        }

        PermanentlyFailedCounter.WithLabels(dispatch.TemplateKey, isMeetingRelated ? "true" : "false").Inc();
        _logger?.LogWarning(
            "email.dispatch.permanently_failed DispatchId={DispatchId} TenantId={TenantId} TemplateKey={TemplateKey} "
            + "IsMeetingRelated={IsMeetingRelated} RetryCount={RetryCount} ErrorCode={ErrorCode} Silent={Silent} CorrelationId={CorrelationId}",
            dispatch.Id, dispatch.TenantId, dispatch.TemplateKey, isMeetingRelated, dispatch.RetryCount, dispatch.ErrorCode, silent, dispatch.CorrelationId);
    }

    private async Task NotifyMeetingAsync(NotificationDispatch dispatch, CancellationToken ct)
    {
        if (dispatch.CausationId is not { } meetingId || dispatch.MeetingAttendeeUserId is not { } attendeeUserId)
        {
            // Meeting-templated mail with no (meetingId, attendeeUserId) attribution — cannot happen from
            // MeetingInviteMailer's own 1:1 dispatch path, but a future producer reusing the same template key
            // without setting both fields must not throw here; the ops log/counter still fire.
            _logger?.LogWarning(
                "email.dispatch.permanently_failed.meeting_attribution_missing DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
            return;
        }

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
}
