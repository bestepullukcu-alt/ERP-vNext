using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What a timer should run on: exactly one of a task and a category.</summary>
public sealed record TimerTarget(Guid? TaskItemId, string? CategoryCode)
{
    public bool Matches(TimerSegment segment) => segment.TaskItemId == TaskItemId && segment.CategoryCode == CategoryCode;
}

/// <summary>How a start went. <see cref="ReasonCode"/> is set only when nothing was started.</summary>
public sealed record TimerStartOutcome(TimerSegment? Running, Guid? StoppedSegmentId, string? ReasonCode, bool Changed)
{
    public static TimerStartOutcome Refused(string reasonCode) => new(null, null, reasonCode, false);
}

/// <summary>Why a person's running segment must close at read time, if it must.</summary>
public enum TimerPendingClose
{
    None = 0,
    LocalMidnight = 1,
    Reconcile = 2,
    SwitchedOff = 3
}

/// <summary>A running segment that must close, why, and where it ends: its local midnight, the invalidating transition,
/// or the moment its legal entity's switch went off (never before it started).</summary>
public sealed record PendingTimerClose(TimerSegment? Segment, TimerPendingClose Kind, DateTimeOffset StopAtUtc)
{
    public static PendingTimerClose Nothing(DateTimeOffset now) => new(null, TimerPendingClose.None, now);
}

public interface ITimerService
{
    /// <summary>D12 — is the timer switched on for the person's legal entity? No primary seat, no legal entity, no switch
    /// row: off.</summary>
    Task<bool> IsEnabledForAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Starts the person's timer on a target, switching any other running segment (D2). Checks nothing about the
    /// target — the caller has (the control command checks holder + lifecycle; the hook knows them from the transition).</summary>
    Task<TimerStartOutcome> StartAsync(
        Guid userId, TimerTarget target, TimerStartSource source, Guid? startTransitionId, CancellationToken ct = default);

    /// <summary>Closes a running segment at <paramref name="requestedStopUtc"/> — or at its local midnight if that came
    /// first (D3) — and writes its draft. <c>false</c> when another writer closed it first.</summary>
    Task<bool> CloseAsync(
        TimerSegment segment, DateTimeOffset requestedStopUtc, TimerStopReason reason, Guid? stopTransitionId,
        CancellationToken ct = default);

    /// <summary>Why the person's running segment must be closed now, if it must — reads only, writes nothing.</summary>
    Task<PendingTimerClose> PendingCloseAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 (pack §19.1, D2, D3, D12) — the timer's mechanics in one place; every door (the control endpoints, the
/// MOD-0024 transition hook, the midnight job, the read-time reconcile) comes through here.
///
/// <para><b>One running segment per person</b> is a storage rule (partial unique index), not a check: a start first
/// closes whatever runs (as a <see cref="TimerStopReason.Switch"/>), then inserts; losing the insert to a concurrent start
/// means reading again and switching again. <b>Server clock only</b> — no instant ever comes from a request.</para>
/// </summary>
public sealed class TimerService : ITimerService
{
    private const int MaxStartAttempts = 4;

    private readonly ITimerSegmentRepository _segments;
    private readonly ILegalEntityTimeSettingRepository _switches;
    private readonly ITimeEntryOrgGateway _org;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly ITimerDraftWriter _drafts;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public TimerService(
        ITimerSegmentRepository segments,
        ILegalEntityTimeSettingRepository switches,
        ITimeEntryOrgGateway org,
        ITimeEntryTaskGateway tasks,
        IWorkingHoursProvider workingHours,
        ITimerDraftWriter drafts,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _segments = segments;
        _switches = switches;
        _org = org;
        _tasks = tasks;
        _workingHours = workingHours;
        _drafts = drafts;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<bool> IsEnabledForAsync(Guid userId, CancellationToken ct = default)
        => (await SwitchOfAsync(userId, ct))?.TimerEnabled == true;

    private async Task<LegalEntityTimeSetting?> SwitchOfAsync(Guid userId, CancellationToken ct)
    {
        var seat = await _org.PrimarySeatAsync(userId, ct);
        return seat?.LegalEntityId is { } legalEntityId
            ? await _switches.GetByLegalEntityIdAsync(legalEntityId, ct)
            : null;
    }

    public async Task<TimerStartOutcome> StartAsync(
        Guid userId, TimerTarget target, TimerStartSource source, Guid? startTransitionId, CancellationToken ct = default)
    {
        // A replayed transition is a no-op, even if the segment it started has since been closed: the hook must never
        // start work a second time (T-02).
        if (startTransitionId is { } transitionId && await _segments.ExistsForStartTransitionAsync(transitionId, ct))
        {
            return new TimerStartOutcome(await _segments.GetRunningAsync(userId, ct), null, null, false);
        }

        Guid? stopped = null;
        Guid? switchedFrom = null;
        for (var attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            var now = _clock.GetUtcNow();
            var running = await _segments.GetRunningAsync(userId, ct);
            if (running is not null)
            {
                var pastMidnight = TimerRules.PastLocalMidnight(running.LocalDate, Zone(running), now);
                if (target.Matches(running) && !pastMidnight)
                {
                    return new TimerStartOutcome(running, stopped, null, false); // already running on it
                }

                if (await CloseAsync(running, now, TimerStopReason.Switch, null, ct))
                {
                    stopped ??= running.Id;
                    // Only a real switch can be undone; a segment its midnight had already ended was not switched away.
                    switchedFrom ??= pastMidnight ? null : running.Id;
                }
            }

            var hours = await _workingHours.GetWorkingWindowsAsync(userId, DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1),
                DateOnly.FromDateTime(now.UtcDateTime).AddDays(1), ct);
            var localDate = hours.LocalDateOf(now);
            // A switch the person may undo: the token lives on the NEW segment, pointing at the one it stopped. An undo
            // itself cannot be undone.
            var undoable = source != TimerStartSource.UndoSwitch && switchedFrom is not null;
            var segment = new TimerSegment
            {
                TenantId = _tenantContext.TenantId,
                UserId = userId,
                TaskItemId = target.TaskItemId,
                CategoryCode = target.CategoryCode,
                StartedAtUtc = now,
                LocalDate = localDate,
                TimeZoneId = hours.TimeZoneId,
                WeekKey = WeekCalendar.KeyOf(localDate),
                StartSource = source,
                StartTransitionId = startTransitionId,
                SwitchedFromSegmentId = undoable ? switchedFrom : null,
                SwitchToken = undoable ? Guid.NewGuid() : null,
                CreatedBy = userId.ToString()
            };

            if (await _segments.TryStartAsync(segment, ct))
            {
                return new TimerStartOutcome(segment, stopped, null, true);
            }

            if (startTransitionId is { } replayed && await _segments.ExistsForStartTransitionAsync(replayed, ct))
            {
                // The same transition, observed concurrently — the other observer started it.
                return new TimerStartOutcome(await _segments.GetRunningAsync(userId, ct), stopped, null, stopped is not null);
            }

            // A concurrent start for this person won the insert: read again, and switch from it.
        }

        return TimerStartOutcome.Refused(TimeEntryReasonCodes.TimerConcurrencyConflict);
    }

    public async Task<bool> CloseAsync(
        TimerSegment segment, DateTimeOffset requestedStopUtc, TimerStopReason reason, Guid? stopTransitionId,
        CancellationToken ct = default)
    {
        if (segment.StartedAtUtc is not { } startedAt)
        {
            return false;
        }

        var zone = Zone(segment);
        var (stopAt, atMidnight) = TimerRules.StopPoint(startedAt, segment.LocalDate, zone, requestedStopUtc);
        var day = (await _workingHours.GetWorkingWindowsAsync(segment.UserId, segment.LocalDate, segment.LocalDate, ct))
            .DayOf(segment.LocalDate);
        var duration = (int)Math.Max(0, Math.Floor((stopAt - startedAt).TotalSeconds));
        var outside = TimerRules.OutsideWorkingMinutes(startedAt, stopAt, day);

        if (!await _segments.TryCloseAsync(
                segment.Id, stopAt, duration, outside, atMidnight ? TimerStopReason.LocalMidnight : reason, stopTransitionId, ct))
        {
            return false;
        }

        await _drafts.ApplyAsync(segment.UserId, segment.LocalDate, segment.TaskItemId, segment.CategoryCode, ct);
        return true;
    }

    public async Task<PendingTimerClose> PendingCloseAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var running = await _segments.GetRunningAsync(userId, ct);
        if (running?.StartedAtUtc is not { } startedAt)
        {
            return PendingTimerClose.Nothing(now);
        }

        if (TimerRules.PastLocalMidnight(running.LocalDate, Zone(running), now))
        {
            return new PendingTimerClose(running, TimerPendingClose.LocalMidnight, now);
        }

        var @switch = await SwitchOfAsync(userId, ct);
        if (@switch?.TimerEnabled != true)
        {
            // D12 — closed at the moment the switch went off (§13); with no row at all (or no legal entity), now.
            var at = @switch is not null && @switch.ChangedAtUtc > startedAt && @switch.ChangedAtUtc < now ? @switch.ChangedAtUtc : now;
            return new PendingTimerClose(running, TimerPendingClose.SwitchedOff, at);
        }

        if (running.TaskItemId is { } taskId)
        {
            var facts = await _tasks.TaskFactsAsync([taskId], ct);
            if (!facts.TryGetValue(taskId, out var task) || !task.IsInProgress || task.HolderUserId != userId)
            {
                // §13 — closed at the invalidating transition's time; a task that vanished (or a log that names nothing)
                // closes now.
                var at = await _tasks.InvalidatedAtAsync(taskId, userId, startedAt, ct);
                return new PendingTimerClose(running, TimerPendingClose.Reconcile, at is { } when && when < now ? when : now);
            }
        }

        return PendingTimerClose.Nothing(now);
    }

    /// <summary>The zone the segment was started in (R3: the tenant's); UTC if it can no longer be resolved.</summary>
    public static TimeZoneInfo Zone(TimerSegment segment)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(segment.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
