using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

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
    private readonly IMediator _mediator;
    private readonly ILogger<TimerService> _logger;

    public TimerService(
        ITimerSegmentRepository segments,
        ILegalEntityTimeSettingRepository switches,
        ITimeEntryOrgGateway org,
        ITimeEntryTaskGateway tasks,
        IWorkingHoursProvider workingHours,
        ITimerDraftWriter drafts,
        ITenantContext tenantContext,
        TimeProvider clock,
        IMediator mediator,
        ILogger<TimerService> logger)
    {
        _mediator = mediator;
        _logger = logger;
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
                var pending = await PendingCloseAsync(userId, ct);
                if (pending.Segment?.Id == running.Id && pending.Kind != TimerPendingClose.None)
                {
                    // The run had already ended — at its midnight, when its task left the person, or when the switch
                    // went off (v2 F2): it closes THERE, and is not a switch anyone could undo.
                    if (await CloseAsync(running, pending.StopAtUtc, ReasonOf(pending.Kind), null, ct))
                    {
                        stopped ??= running.Id;
                    }
                }
                else if (target.Matches(running))
                {
                    return new TimerStartOutcome(running, stopped, null, false); // already running on it
                }
                else if (await CloseAsync(running, now, TimerStopReason.Switch, null, ct))
                {
                    stopped ??= running.Id;
                    switchedFrom ??= running.Id; // only a real switch can be undone
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

        // The segment is closed and stays closed whatever happens next. Its draft is a RECOMPUTATION from the segments,
        // so a draft that cannot be written now is not lost (v2 F5): the next save, submit or midnight run of this week
        // writes it, and the week read shows the difference until then. A close therefore never fails on its draft.
        try
        {
            var outcome = await _drafts.ApplyAsync(segment.UserId, segment.LocalDate, segment.TaskItemId, segment.CategoryCode, ct);
            if (outcome == TimerDraftOutcome.WeekNotWritable)
            {
                // v2 F3 / D4 — the week is already submitted or approved (or out of reach): its approval will not come
                // back for this segment, so its instants are cleared now, by the same audited command.
                await _mediator.Send(
                    new MinimiseTimerSegmentsCommand(segment.Id, segment.UserId, segment.WeekKey, segment.Id.ToString(), segment.Id), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "time-entry.timer.draft_failed SegmentId={SegmentId}; the next save, submit or midnight run recomputes it.", segment.Id);
        }

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

        // Every reason the run may already have ended, each with the instant it ended at; the EARLIEST wins (v2 F2). A
        // task completed at 18:00 and read after midnight closes at 18:00 (Reconcile), not at 00:00.
        var candidates = new List<(DateTimeOffset At, TimerPendingClose Kind)>();

        if (running.TaskItemId is { } taskId)
        {
            var facts = await _tasks.TaskFactsAsync([taskId], ct);
            if (!facts.TryGetValue(taskId, out var task) || !task.IsInProgress || task.HolderUserId != userId)
            {
                // §13 — the invalidating transition's time, read through the task port; a task that vanished (or a log
                // that names nothing) ends now.
                var at = await _tasks.InvalidatedAtAsync(taskId, userId, startedAt, ct);
                candidates.Add((Clamp(at ?? now, startedAt, now), TimerPendingClose.Reconcile));
            }
        }

        var @switch = await SwitchOfAsync(userId, ct);
        if (@switch?.TimerEnabled != true)
        {
            // D12 — closed at the moment the switch went off (§13); with no row at all (or no legal entity), now.
            var at = @switch is not null && @switch.ChangedAtUtc > startedAt ? @switch.ChangedAtUtc : now;
            candidates.Add((Clamp(at, startedAt, now), TimerPendingClose.SwitchedOff));
        }

        var midnight = TimerRules.LocalMidnightAfter(running.LocalDate, Zone(running));
        if (now >= midnight)
        {
            candidates.Add((midnight, TimerPendingClose.LocalMidnight));
        }

        if (candidates.Count == 0)
        {
            return PendingTimerClose.Nothing(now);
        }

        // Earliest first; on a tie the task or the switch is the truer reason than the clock.
        var (stopAt, kind) = candidates.OrderBy(c => c.At.UtcTicks).ThenBy(c => c.Kind == TimerPendingClose.LocalMidnight).First();
        return new PendingTimerClose(running, kind, stopAt);
    }

    private static DateTimeOffset Clamp(DateTimeOffset value, DateTimeOffset min, DateTimeOffset max)
        => value < min ? min : value > max ? max : value;

    /// <summary>The stop reason a pending close is recorded with.</summary>
    public static TimerStopReason ReasonOf(TimerPendingClose kind) => kind switch
    {
        TimerPendingClose.LocalMidnight => TimerStopReason.LocalMidnight,
        TimerPendingClose.SwitchedOff => TimerStopReason.SwitchedOff,
        _ => TimerStopReason.Reconcile
    };

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
