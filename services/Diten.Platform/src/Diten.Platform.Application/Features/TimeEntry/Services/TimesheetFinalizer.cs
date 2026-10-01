using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>What one finalization run did to the week.</summary>
public enum TimesheetFinalizationResult
{
    /// <summary>The week is not a submitted week with an approval — nothing to finalize.</summary>
    NotApplicable = 0,

    /// <summary>MOD-0023 has not decided yet.</summary>
    NoDecision = 1,

    Approved = 2,
    Rejected = 3,

    /// <summary>The instance was cancelled (or vanished) outside this module; the week went back to Draft.</summary>
    Returned = 4,

    /// <summary>MOD-0023 recorded the person as the one who decided their OWN week; the decision was refused and the
    /// week went back to Draft, saying why (F2).</summary>
    SelfDecisionReturned = 5,

    /// <summary>This outcome was already applied (a replay) — the week and the totals are left as they are.</summary>
    AlreadyApplied = 6,

    /// <summary>F12 — the week was already approved; its outstanding task totals were written now.</summary>
    TotalsApplied = 7
}

/// <summary>The synthetic integration event the finalizer is keyed by (C-8). Never published; it exists so the
/// consumed-event store can say "this outcome of this instance was already applied".</summary>
public sealed record TimesheetDecisionObserved(Guid WorkflowInstanceId, string Outcome) : IIntegrationEvent
{
    public const string Name = "timeentry.timesheet.decided.v1";

    public string EventName => Name;

    public int EventVersion => 1;
}

public interface ITimesheetFinalizer
{
    /// <summary>Is there a MOD-0023 decision this week has not taken on yet? Cheap; writes nothing.</summary>
    Task<bool> HasPendingOutcomeAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>Applies MOD-0023's outcome to the week, if there is one, and recomputes the affected task totals.</summary>
    Task<TimesheetFinalizationResult> FinalizeAsync(Guid weekId, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D7 / R4 — the PULL-BASED approval finalizer. MOD-0023 emits no decision event, so this reads the
/// outcome and applies it: approved → the week locks and comes into force (a corrected original is superseded), and
/// <see cref="TaskTimeTotal"/> is RECOMPUTED; rejected → back to Draft with the approver's reason.
///
/// <para><b>Where it runs.</b> On the person's week read, on the approvals page, and in the sweep job. None of them is
/// required: correctness never depends on a scheduler (pack §8.4).</para>
///
/// <para><b>Idempotency, twice over.</b> (1) The consumed-event store keys each (instance, outcome) by a deterministic
/// id (C-8). (2) The week's own transition is a conditional write from Submitted-with-this-instance, so even if the
/// store is lost the outcome lands once. The totals are a recomputation — a replay writes the same numbers (T-14).</para>
/// </summary>
public sealed class TimesheetFinalizer : ITimesheetFinalizer
{
    public const string ConsumerName = "time-entry.timesheet-finalizer";

    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly ITaskTimeTotalRepository _totals;
    private readonly ITimesheetApprovalService _approvals;
    private readonly ConsumedEventStore _consumedEvents;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;
    private readonly ITimesheetFinalizationProbe _probe;
    private readonly ITimerSegmentRepository _segments;
    private readonly IMediator _mediator;
    private readonly ITimeEntryNotifier _notifier;
    private readonly ILogger<TimesheetFinalizer> _logger;

    public TimesheetFinalizer(
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ITaskTimeTotalRepository totals,
        ITimesheetApprovalService approvals,
        ConsumedEventStore consumedEvents,
        ITenantContext tenantContext,
        TimeProvider clock,
        ITimesheetFinalizationProbe probe,
        ITimerSegmentRepository segments,
        IMediator mediator,
        ITimeEntryNotifier notifier,
        ILogger<TimesheetFinalizer> logger)
    {
        _notifier = notifier;
        _segments = segments;
        _mediator = mediator;
        _weeks = weeks;
        _entries = entries;
        _totals = totals;
        _approvals = approvals;
        _consumedEvents = consumedEvents;
        _tenantContext = tenantContext;
        _clock = clock;
        _probe = probe;
        _logger = logger;
    }

    public async Task<bool> HasPendingOutcomeAsync(TimesheetWeek week, CancellationToken ct = default)
    {
        // F12 — approved, but its task totals never landed: that is outstanding work too.
        if (NeedsTotals(week))
        {
            return true;
        }

        if (week.Status != TimesheetWeekStatus.Submitted || week.WorkflowInstanceId is not { } instanceId)
        {
            return false;
        }

        return (await _approvals.ReadDecisionAsync(instanceId, ct)).Outcome != TimesheetDecisionOutcome.Pending;
    }

    public async Task<TimesheetFinalizationResult> FinalizeAsync(Guid weekId, CancellationToken ct = default)
    {
        var week = await _weeks.GetByIdAsync(weekId, ct);
        if (week is not null && NeedsTotals(week))
        {
            await ApplyTotalsAsync(week, _clock.GetUtcNow(), ct);
            return TimesheetFinalizationResult.TotalsApplied;
        }

        if (week is null || week.Status != TimesheetWeekStatus.Submitted || week.WorkflowInstanceId is not { } instanceId)
        {
            return TimesheetFinalizationResult.NotApplicable;
        }

        var decision = await _approvals.ReadDecisionAsync(instanceId, ct);
        if (decision.Outcome == TimesheetDecisionOutcome.Pending)
        {
            return TimesheetFinalizationResult.NoDecision;
        }

        var result = TimesheetFinalizationResult.AlreadyApplied;
        var envelope = Envelope(instanceId, OutcomeKey(week, decision));
        var execution = await _consumedEvents.ExecuteOnceAsync(
            envelope,
            ConsumerName,
            async token => result = await ApplyAsync(weekId, instanceId, decision, token),
            ct);

        if (execution == ConsumedEventExecutionResult.Duplicate)
        {
            // The store says this outcome was applied (or is being applied). The week's own state is the truth: if it
            // is still Submitted on this instance, the earlier run did not get as far as the week — apply it now. The
            // conditional writes inside ApplyAsync make this safe against a concurrent run.
            var fresh = await _weeks.GetByIdAsync(weekId, ct);
            if (fresh is { Status: TimesheetWeekStatus.Submitted } && fresh.WorkflowInstanceId == instanceId)
            {
                result = await ApplyAsync(weekId, instanceId, decision, ct);
            }
        }

        return result;
    }

    /// <summary>A decision by the person about their OWN week is not a decision (F2) — keyed apart from a real one.</summary>
    private static bool IsSelfDecision(TimesheetWeek week, TimesheetDecision decision)
        => decision.Outcome is TimesheetDecisionOutcome.Approved or TimesheetDecisionOutcome.Rejected
           && decision.ActorUserId == week.UserId;

    private static string OutcomeKey(TimesheetWeek week, TimesheetDecision decision)
        => IsSelfDecision(week, decision) ? "SelfDecision" : decision.Outcome.ToString();

    private async Task<TimesheetFinalizationResult> ApplyAsync(
        Guid weekId, Guid instanceId, TimesheetDecision decision, CancellationToken ct)
    {
        var week = await _weeks.GetByIdAsync(weekId, ct);
        if (week is null || week.Status != TimesheetWeekStatus.Submitted || week.WorkflowInstanceId != instanceId)
        {
            return TimesheetFinalizationResult.AlreadyApplied;
        }

        var now = _clock.GetUtcNow();

        if (IsSelfDecision(week, decision))
        {
            // Pack §13 / F2: resolution never names the person, but MOD-0023 recorded THEM as the one who decided (a
            // delegation back to them, say). Neither their approval nor their rejection is taken on: whatever is still
            // open in MOD-0023 is cancelled, and the week goes back to the person as a Draft saying why — never left
            // Submitted, where nobody could ever move it again.
            await _approvals.CancelAsync(instanceId, week.UserId, ct);
            week.Status = TimesheetWeekStatus.Draft;
            week.WorkflowInstanceId = null;
            week.FinalizationBlockedReason = TimeEntryReasonCodes.SelfDecisionRefused;
            EnsureWritten(await _weeks.UpdateAsync(week, week.Version, ct), week.Id);
            _logger.LogWarning("Week {WeekId} was decided by its own owner in MOD-0023; returned to Draft.", week.Id);
            return TimesheetFinalizationResult.SelfDecisionReturned;
        }

        switch (decision.Outcome)
        {
            case TimesheetDecisionOutcome.Approved:
                return await ApproveAsync(week, decision, now, ct);

            case TimesheetDecisionOutcome.Rejected:
                week.Status = TimesheetWeekStatus.Draft;
                week.WorkflowInstanceId = null;
                week.LastRejectedAtUtc = decision.DecidedAtUtc ?? now;
                week.LastRejectedByUserId = decision.ActorUserId;
                week.LastRejectionReason = decision.Comment;
                week.FinalizationBlockedReason = null;
                EnsureWritten(await _weeks.UpdateAsync(week, week.Version, ct), week.Id);
                // T3 (N4) — only here, where THIS run moved the week from Submitted: a replay never reaches this line.
                await _notifier.WeekRejectedAsync(week, ct);
                return TimesheetFinalizationResult.Rejected;

            default:
                // Cancelled or missing outside this module (our own withdraw clears the instance first, so it never
                // arrives here): the person gets their week back as a Draft.
                week.Status = TimesheetWeekStatus.Draft;
                week.WorkflowInstanceId = null;
                week.FinalizationBlockedReason = null;
                EnsureWritten(await _weeks.UpdateAsync(week, week.Version, ct), week.Id);
                return TimesheetFinalizationResult.Returned;
        }
    }

    /// <summary>
    /// F9 — safe to run again after a crash at ANY point. The revision a correction replaces is found by its revision
    /// number, not by "is in force": if an earlier run superseded it and then died, the rerun still finds it, still
    /// recomputes the tasks only IT had, and simply skips the supersede that already happened.
    /// </summary>
    private async Task<TimesheetFinalizationResult> ApproveAsync(
        TimesheetWeek week, TimesheetDecision decision, DateTimeOffset now, CancellationToken ct)
    {
        var revisions = await _weeks.ListRevisionsAsync(week.UserId, week.WeekKey, ct);
        var previous = revisions.FirstOrDefault(r => r.Id != week.Id
            && (r.InForce || (week.CorrectionOfRevision == r.RevisionNumber && r.Status == TimesheetWeekStatus.Superseded)));

        // A correction supersedes the revision in force — FIRST, because the in-force index allows only one.
        if (previous is { InForce: true })
        {
            previous.InForce = false;
            previous.Status = TimesheetWeekStatus.Superseded;
            previous.SupersededAtUtc = now;
            EnsureWritten(await _weeks.UpdateAsync(previous, previous.Version, ct), previous.Id);
        }

        week.Status = TimesheetWeekStatus.Approved;
        week.InForce = true;
        week.IsOpen = false;
        week.ApprovedAtUtc = decision.DecidedAtUtc ?? now;
        week.ApprovedByUserId = decision.ActorUserId;
        week.ReopenActive = false; // a reopen lasts until approval (F7)
        week.FinalizationBlockedReason = null;
        EnsureWritten(await _weeks.UpdateAsync(week, week.Version, ct), week.Id);

        // T3 (N4) — the state change happened here and only here; told BEFORE the totals, so a totals failure (retried by
        // F12, which never re-enters this method) cannot cost the person the e-mail.
        await _notifier.WeekApprovedAsync(week, ct);

        await ApplyTotalsAsync(week, now, ct);
        return TimesheetFinalizationResult.Approved;
    }

    /// <summary>F12 — an approved revision whose totals were never written (the week write landed, the totals did not).</summary>
    public static bool NeedsTotals(TimesheetWeek week)
        => week.Status == TimesheetWeekStatus.Approved && week.TotalsAppliedAtUtc is null;

    /// <summary>
    /// Recomputes every task the revision OR the revision it corrects touched, minimises the week's timer segments (D4),
    /// then marks the week. The mark is written
    /// LAST: a failure anywhere before it leaves the week "approved, totals outstanding", which every retry path
    /// (read, approvals page, sweep) picks up — the week is never left approved with totals that nobody will fix (F12).
    /// </summary>
    private async Task ApplyTotalsAsync(TimesheetWeek week, DateTimeOffset now, CancellationToken ct)
    {
        var touched = (await _entries.ListByWeekAsync(week.Id, ct)).ToList();
        if (week.CorrectionOfRevision is { } corrected)
        {
            var previous = (await _weeks.ListRevisionsAsync(week.UserId, week.WeekKey, ct))
                .FirstOrDefault(r => r.Id != week.Id && r.RevisionNumber == corrected);
            if (previous is not null)
            {
                touched.AddRange(await _entries.ListByWeekAsync(previous.Id, ct));
            }
        }

        var taskIds = touched
            .Where(e => e.TaskItemId is not null)
            .Select(e => e.TaskItemId!.Value)
            .ToHashSet();
        await RecomputeTaskTotalsAsync(taskIds, week.Id, now, ct);

        // D4 (T1b) — the approved week's timer segments lose their start/stop instants: an audited update, never a delete.
        // BEFORE the mark, so a crash in between is retried by the same F12 path; replay-safe, because only segments
        // still carrying their instants are touched — and nothing is sent (or audited) when there are none.
        if (await _segments.CountUnminimisedAsync(week.UserId, week.WeekKey, ct) > 0)
        {
            var minimised = await _mediator.Send(
                new MinimiseTimerSegmentsCommand(week.Id, week.UserId, week.WeekKey, week.Id.ToString()), ct);
            if (!minimised.IsSuccessful)
            {
                throw new InvalidOperationException($"The timer segments of week {week.Id} could not be minimised; finalization will retry.");
            }
        }

        week.TotalsAppliedAtUtc = now;
        EnsureWritten(await _weeks.UpdateAsync(week, week.Version, ct), week.Id);
    }

    /// <summary>
    /// D7 — each task's approved minutes = the sum of its entries in APPROVED, IN-FORCE revisions. A recomputation from
    /// the source rows, never an increment of the stored figure, so running it again writes the same numbers.
    ///
    /// <para><b>F9 — two finalizers never overwrite each other.</b> The total's version is read BEFORE the approvals are
    /// read, and the write is conditional on it. A finalizer that computed from an older view (another week of the same
    /// task was approved in between) loses the write, reads again and recomputes.</para>
    /// </summary>
    private async Task RecomputeTaskTotalsAsync(
        IReadOnlyCollection<Guid> taskIds, Guid finalizedWeekId, DateTimeOffset now, CancellationToken ct)
    {
        const int maxAttempts = 5;
        foreach (var taskId in taskIds)
        {
            var written = false;
            for (var attempt = 0; attempt < maxAttempts && !written; attempt++)
            {
                var expected = (await _totals.ListByTaskIdsAsync([taskId], ct)).FirstOrDefault()?.Version;

                var rows = await _entries.ListByTaskIdsAsync([taskId], ct);
                var weeks = await _weeks.ListByIdsAsync(rows.Select(r => r.TimesheetWeekId).Distinct().ToList(), ct);
                var inForce = weeks
                    .Where(w => w.InForce && w.Status == TimesheetWeekStatus.Approved)
                    .Select(w => w.Id)
                    .ToHashSet();
                var approvedMinutes = rows
                    .Where(r => r.TaskItemId == taskId && inForce.Contains(r.TimesheetWeekId))
                    .Sum(r => r.DurationMinutes);

                await _probe.BeforeTaskTotalWriteAsync(taskId, ct);
                written = await _totals.TrySetApprovedMinutesAsync(taskId, approvedMinutes, finalizedWeekId, now, expected, ct);
            }

            if (!written)
            {
                throw new InvalidOperationException($"The approved total of task {taskId} kept changing; finalization will retry.");
            }
        }
    }

    private static void EnsureWritten(bool written, Guid weekId)
    {
        if (!written)
        {
            // A concurrent writer moved the row. Throwing marks this outcome failed in the consumed-event store, so
            // the next read, approvals page or sweep retries it.
            throw new InvalidOperationException($"Timesheet week {weekId} changed while its approval was being finalized.");
        }
    }

    private EventEnvelope<TimesheetDecisionObserved> Envelope(Guid instanceId, string outcome)
    {
        var payload = new TimesheetDecisionObserved(instanceId, outcome);
        var eventId = DeterministicId($"{TimesheetDecisionObserved.Name}|{instanceId:N}|{outcome}");
        return new EventEnvelope<TimesheetDecisionObserved>(
            new EventMetadata(
                EventId: eventId,
                EventName: TimesheetDecisionObserved.Name,
                EventVersion: 1,
                CorrelationId: eventId,
                CausationId: instanceId,
                TenantId: _tenantContext.TenantId,
                Producer: "Diten.Platform/time-entry",
                OccurredAtUtc: _clock.GetUtcNow()),
            payload);
    }

    /// <summary>The same input always gives the same id: the first 16 bytes of its SHA-256.</summary>
    internal static Guid DeterministicId(string value)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
