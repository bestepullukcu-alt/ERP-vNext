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

    /// <summary>The claim's <c>UpdatedBy</c>: this prefix, the attempt number, the claiming host and a nonce.</summary>
    public const string ClaimActorPrefix = "permanent-failure-effects#";

    /// <summary>After this many attempts on one row the effects are given up — named, counted, logged.</summary>
    public const int MaxAttempts = 5;

    /// <summary>How often one run tries to write its mark before it leaves the row pending (C-FIX1).</summary>
    public const int MarkAttempts = 3;

    private static readonly Counter EffectsGivenUpCounter = Metrics.CreateCounter(
        "notification_dispatch_permanent_effects_given_up",
        "Permanent-failure effects (organizer notification, attendee badge) that kept failing and were given up.",
        new CounterConfiguration { LabelNames = new[] { "template_key" } });

    private static readonly Counter MarkFailedCounter = Metrics.CreateCounter(
        "notification_dispatch_permanent_effects_mark_failed",
        "Permanent-failure effects that ran but whose done-mark could not be written; the row stays pending.",
        new CounterConfiguration { LabelNames = new[] { "template_key" } });

    /// <summary>
    /// BL-454 — the effects of a PENDING row:
    /// <list type="number">
    ///   <item>CLAIM (<see cref="INotificationDispatchRepository.TryClaimPermanentEffectsAsync"/>): a targeted write,
    ///     conditional on the version read and on the row still being pending, stamping who (<c>UpdatedBy</c>: attempt,
    ///     host, nonce) and when (<c>UpdatedAt</c>). A second run that read the same version loses here and does nothing.</item>
    ///   <item>RUN the meeting effects (attendee badge, organizer notification). If they fail, nothing is marked — the row
    ///     is re-driven once its grace has passed. Past <see cref="MaxAttempts"/> attempts the effects are given up, named
    ///     and counted, and no longer run at all.</item>
    ///   <item>COUNT: the permanent-failure counter and log line, once per completion, whatever the effects did (C-FIX1 4).</item>
    ///   <item>MARK (<see cref="INotificationDispatchRepository.TryMarkPermanentEffectsAppliedAsync"/>): a targeted
    ///     <c>$set</c> of the real time, conditional on the row still pending and the claim still ours. Lost or failed, it is
    ///     re-read and tried again (<see cref="MarkAttempts"/>); a mark that cannot be written is named, counted and logged,
    ///     and the row stays pending (C-FIX1 1).</item>
    /// </list>
    /// <b>When the effects can run twice</b> — only after a claim went stale (its run died or hung past the grace, or its
    /// mark could not be written): the re-claim runs them again. Every such run is an attempt, so it is bounded by
    /// <see cref="MaxAttempts"/> and each one is counted and logged.
    /// </summary>
    /// <returns>True when this call completed the row (effects run or given up).</returns>
    public async Task<bool> ApplyAndMarkAsync(
        NotificationDispatch dispatch, bool silent, INotificationDispatchRepository repository, CancellationToken ct)
    {
        if (!NotificationDispatch.IsPermanentFailurePending(dispatch) || dispatch.Status != NotificationDispatchStatus.Failed)
        {
            return false;
        }

        var attempt = NextAttempt(dispatch.UpdatedBy);
        var readVersion = dispatch.Version;
        var claimActor = $"{ClaimActorPrefix}{attempt}:{Environment.MachineName}:{Guid.NewGuid():N}";
        var claimedAt = DateTimeOffset.UtcNow;
        if (!await repository.TryClaimPermanentEffectsAsync(dispatch, readVersion, claimedAt, claimActor, ct))
        {
            _logger?.LogInformation(
                "email.dispatch.permanently_failed.claim_lost DispatchId={DispatchId} TenantId={TenantId}",
                dispatch.Id, dispatch.TenantId);
            return false;
        }

        dispatch.UpdatedAt = claimedAt;
        dispatch.UpdatedBy = claimActor;
        dispatch.Version = readVersion + 1;

        var givenUp = false;
        if (attempt > MaxAttempts)
        {
            // Earlier runs ran the effects (or tried to) and could not mark: no further run of them.
            givenUp = GiveUp(dispatch, attempt, null, "AttemptsExhausted");
        }
        else
        {
            try
            {
                await NotifyIfDueAsync(dispatch, silent, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < MaxAttempts)
                {
                    // Not marked, not counted: still pending, re-driven after the grace period.
                    _logger?.LogWarning(
                        ex,
                        "email.dispatch.permanently_failed.effects_failed DispatchId={DispatchId} TenantId={TenantId} Attempt={Attempt} MaxAttempts={MaxAttempts}",
                        dispatch.Id, dispatch.TenantId, attempt, MaxAttempts);
                    return false;
                }

                givenUp = GiveUp(dispatch, attempt, ex, "EffectsKeptFailing");
            }
        }

        CountPermanentFailure(dispatch, silent, givenUp);
        await MarkAsync(dispatch, claimActor, repository, ct);
        return true;
    }

    private bool GiveUp(NotificationDispatch dispatch, int attempt, Exception? ex, string reason)
    {
        EffectsGivenUpCounter.WithLabels(dispatch.TemplateKey).Inc();
        _logger?.LogError(
            ex,
            "email.dispatch.permanently_failed.effects_given_up DispatchId={DispatchId} TenantId={TenantId} Attempt={Attempt} Reason={Reason}",
            dispatch.Id, dispatch.TenantId, attempt, reason);
        return true;
    }

    // C-FIX1 1 — the mark: re-read and retried while the row is still pending and the claim still ours; a mark that will
    // not be written is named, counted and logged (the row then stays pending, and its next claim is the next attempt).
    private async Task MarkAsync(NotificationDispatch dispatch, string claimActor, INotificationDispatchRepository repository, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var mark = 1; mark <= MarkAttempts; mark++)
        {
            try
            {
                var appliedAt = DateTimeOffset.UtcNow;
                if (await repository.TryMarkPermanentEffectsAppliedAsync(dispatch, claimActor, appliedAt, ct))
                {
                    dispatch.PermanentlyFailedNotifiedAt = appliedAt;
                    return;
                }

                _logger?.LogWarning(
                    "email.dispatch.permanently_failed.mark_lost DispatchId={DispatchId} TenantId={TenantId} MarkAttempt={MarkAttempt}",
                    dispatch.Id, dispatch.TenantId, mark);
                var current = await repository.GetByIdForTenantAsync(dispatch.TenantId, dispatch.Id, ct);
                if (current is null
                    || !NotificationDispatch.IsPermanentFailurePending(current)
                    || !string.Equals(current.UpdatedBy, claimActor, StringComparison.Ordinal))
                {
                    // Marked by someone else, or the claim was taken over: nothing of ours is left to write.
                    return;
                }

                dispatch.Version = current.Version;
                dispatch.PermanentlyFailedNotifiedAt = current.PermanentlyFailedNotifiedAt;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger?.LogWarning(
                    ex,
                    "email.dispatch.permanently_failed.mark_error DispatchId={DispatchId} TenantId={TenantId} MarkAttempt={MarkAttempt}",
                    dispatch.Id, dispatch.TenantId, mark);
            }
        }

        MarkFailedCounter.WithLabels(dispatch.TemplateKey).Inc();
        _logger?.LogError(
            lastError,
            "email.dispatch.permanently_failed.mark_failed DispatchId={DispatchId} TenantId={TenantId} Reason=MarkKeptFailing",
            dispatch.Id, dispatch.TenantId);
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
    /// The effects where nothing can re-drive them (a first send on a server where no retry job runs): the meeting part
    /// runs once and a failure of it is logged by name; the counter and the log line are written in every case (C-FIX1 4).
    /// </summary>
    public async Task ApplyAsync(NotificationDispatch dispatch, bool silent, CancellationToken ct)
    {
        try
        {
            await NotifyIfDueAsync(dispatch, silent, ct);
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

        CountPermanentFailure(dispatch, silent, givenUp: false);
    }

    // BL-406 — for meeting mail the attendee's "mail undelivered" badge and the organizer's notification (rule 1/2). A
    // SILENT close (a row far older than the retry window, closed the first time the jobs run) tells nobody about mail that
    // died long ago. Throws when a store write fails: the caller decides whether that is retried (pending) or logged.
    private async Task NotifyIfDueAsync(NotificationDispatch dispatch, bool silent, CancellationToken ct)
    {
        if (IsMeetingRelated(dispatch) && !silent)
        {
            await NotifyMeetingAsync(dispatch, ct);
        }
    }

    private static bool IsMeetingRelated(NotificationDispatch dispatch) =>
        dispatch.TemplateKey.StartsWith(MeetingTemplateKeyPrefix, StringComparison.OrdinalIgnoreCase);

    // BL-406 rule 3 / C-FIX1 4 — EVERY permanent failure, meeting or not, silent or not, effects done or given up: the ops
    // counter and the log line, once per completion of the row.
    private void CountPermanentFailure(NotificationDispatch dispatch, bool silent, bool givenUp)
    {
        var isMeetingRelated = IsMeetingRelated(dispatch);
        PermanentlyFailedCounter.WithLabels(dispatch.TemplateKey, isMeetingRelated ? "true" : "false").Inc();
        _logger?.LogWarning(
            "email.dispatch.permanently_failed DispatchId={DispatchId} TenantId={TenantId} TemplateKey={TemplateKey} "
            + "IsMeetingRelated={IsMeetingRelated} RetryCount={RetryCount} ErrorCode={ErrorCode} Silent={Silent} EffectsGivenUp={EffectsGivenUp} CorrelationId={CorrelationId}",
            dispatch.Id, dispatch.TenantId, dispatch.TemplateKey, isMeetingRelated, dispatch.RetryCount, dispatch.ErrorCode, silent, givenUp, dispatch.CorrelationId);
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
