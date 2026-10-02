using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>
/// What one pull did (BL-483). The three numbers are kept apart on purpose: a finalization that failed and was swallowed
/// is NOT a change, and a caller that counts changes (the sweep) must not count it as one.
/// </summary>
/// <param name="Applied">Weeks whose finalization THIS pull completed: approved, rejected, returned to Draft, or an
/// approved week's outstanding totals. A week whose approval was written but whose totals then failed is NOT counted
/// here — it is a failure of this pull, and the pull that lands its totals counts it.</param>
/// <param name="Failed">Weeks whose outcome could not be read or whose finalization failed, swallowed (F14); they are
/// retried by the next read, the approvals page or the sweep.</param>
/// <param name="Attempted">Weeks a finalization was started for, whatever came of it.</param>
public sealed record TimesheetPullResult(int Applied, int Failed, int Attempted)
{
    public static readonly TimesheetPullResult Nothing = new(0, 0, 0);

    /// <summary>Should the caller read the weeks again? Yes whenever a finalization was started or failed: the week may
    /// have moved all the way, part of the way (F14), or under a concurrent finalizer that won.</summary>
    public bool ShouldReread => Attempted > 0 || Failed > 0;
}

public interface ITimesheetDecisionPuller
{
    /// <summary>Finalizes every submitted week in <paramref name="weeks"/> whose MOD-0023 outcome is waiting, and says
    /// what happened. A read re-reads on <see cref="TimesheetPullResult.ShouldReread"/>; the sweep counts
    /// <see cref="TimesheetPullResult.Applied"/> and <see cref="TimesheetPullResult.Failed"/> separately.</summary>
    Task<TimesheetPullResult> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// The read-time door to the finalizer (D7: "it runs on week read, on the approvals page and in the sweep job"). It
/// only SENDS the audited <see cref="FinalizeTimesheetDecisionCommand"/> when an outcome (or an approved week's missing
/// totals, F12) is actually waiting, so a page load that finds nothing to do writes no audit entry — and it never lets
/// a finalization failure turn a read into a 500 (F14).
///
/// <para>BL-484 — "is an outcome waiting?" is asked ONCE for the whole queue (one MOD-0023 read), not once per week.
/// The finalizer re-reads the week and its decision before it writes anything, so an answer that went stale between
/// the question and the command costs nothing but a no-op.</para>
///
/// <para>CT acceptance — when that ONE read fails, the queue is asked week by week, as it was before BL-484. The sweep
/// takes the same oldest weeks every run, so a single instance that cannot be read would otherwise hold every other
/// week of the tenant back for ever; asked one by one, only that week is skipped.</para>
/// </summary>
public sealed class TimesheetDecisionPuller : ITimesheetDecisionPuller
{
    /// <summary>The reason code of a failure that was an exception rather than a refused command.</summary>
    public const string ThrewReasonCode = "FINALIZE_THREW";

    /// <summary>The reason code of a failed "is an outcome waiting?" read.</summary>
    public const string OutcomeReadFailedReasonCode = "OUTCOME_READ_FAILED";

    private readonly ITimesheetFinalizer _finalizer;
    private readonly IMediator _mediator;
    private readonly ILogger<TimesheetDecisionPuller> _logger;

    public TimesheetDecisionPuller(ITimesheetFinalizer finalizer, IMediator mediator, ILogger<TimesheetDecisionPuller> logger)
    {
        _finalizer = finalizer;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<TimesheetPullResult> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default)
    {
        var candidates = weeks.Where(w => w.Status == TimesheetWeekStatus.Submitted || TimesheetFinalizer.NeedsTotals(w)).ToList();
        if (candidates.Count == 0)
        {
            return TimesheetPullResult.Nothing;
        }

        IReadOnlySet<Guid>? waiting;
        try
        {
            waiting = await _finalizer.PendingOutcomeWeekIdsAsync(candidates, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // F14 — the read stands. The shared question failed; each week is asked on its own below.
            LogFailure(null, OutcomeReadFailedReasonCode, ex, correlationId);
            waiting = null;
        }

        var applied = 0;
        var failed = 0;
        var attempted = 0;
        foreach (var week in candidates)
        {
            try
            {
                if (!(waiting?.Contains(week.Id) ?? await _finalizer.HasPendingOutcomeAsync(week, ct)))
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Only THIS week's outcome could not be read; the rest of the queue goes on.
                failed++;
                LogFailure(week.Id, OutcomeReadFailedReasonCode, ex, correlationId);
                continue;
            }

            attempted++;
            try
            {
                var response = await _mediator.Send(new FinalizeTimesheetDecisionCommand(week.Id, correlationId), ct);
                if (!response.IsSuccessful)
                {
                    failed++;
                    LogFailure(week.Id, response.ReasonCode ?? "FINALIZE_REFUSED", null, correlationId);
                }
                else if (WroteState(response.Data))
                {
                    applied++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // F14 — a READ never fails because a finalization did: a concurrent finalizer that won, a store error, a
                // totals conflict. This week is logged and skipped (the next read, the approvals page or the sweep takes
                // it again). BL-483 — it is counted as a FAILURE, never as a change.
                failed++;
                LogFailure(week.Id, ThrewReasonCode, ex, correlationId);
            }
        }

        return new TimesheetPullResult(applied, failed, attempted);
    }

    /// <summary>Did the finalizer write the week's state in THIS run? No decision yet, a replay and "not a submitted
    /// week any more" are runs that changed nothing.</summary>
    internal static bool WroteState(string? finalizationResult)
        => Enum.TryParse<TimesheetFinalizationResult>(finalizationResult, out var result)
           && result is TimesheetFinalizationResult.Approved
               or TimesheetFinalizationResult.Rejected
               or TimesheetFinalizationResult.Returned
               or TimesheetFinalizationResult.SelfDecisionReturned
               or TimesheetFinalizationResult.TotalsApplied;

    /// <summary>
    /// The LINE carries the week id, a reason code and the exception's type — fields a search can count — and never
    /// the exception's text. The exception itself goes to the log beside it, as it did before BL-483: every finalizer
    /// failure is an <see cref="InvalidOperationException"/>, and without its message and stack "the week changed",
    /// "the totals kept changing" and "the store is down" are one indistinguishable line.
    /// </summary>
    private void LogFailure(Guid? weekId, string reasonCode, Exception? exception, string correlationId)
        => _logger.LogWarning(
            exception,
            "time-entry.decision.pull.failed WeekId={WeekId} ReasonCode={ReasonCode} ExceptionType={ExceptionType} "
            + "CorrelationId={CorrelationId}; skipped, will be retried.",
            weekId, reasonCode, exception?.GetType().Name, correlationId);
}
