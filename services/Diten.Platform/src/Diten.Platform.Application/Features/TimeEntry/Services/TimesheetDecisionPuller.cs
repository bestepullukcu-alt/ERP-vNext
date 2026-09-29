using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

public interface ITimesheetDecisionPuller
{
    /// <summary>Finalizes every submitted week in <paramref name="weeks"/> whose MOD-0023 outcome is waiting. True when
    /// at least one was finalized (the caller re-reads).</summary>
    Task<bool> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// The read-time door to the finalizer (D7: "it runs on week read, on the approvals page and in the sweep job"). It
/// only SENDS the audited <see cref="FinalizeTimesheetDecisionCommand"/> when an outcome (or an approved week's missing
/// totals, F12) is actually waiting, so a page load that finds nothing to do writes no audit entry — and it never lets
/// a finalization failure turn a read into a 500 (F14).
/// </summary>
public sealed class TimesheetDecisionPuller : ITimesheetDecisionPuller
{
    private readonly ITimesheetFinalizer _finalizer;
    private readonly IMediator _mediator;
    private readonly ILogger<TimesheetDecisionPuller> _logger;

    public TimesheetDecisionPuller(ITimesheetFinalizer finalizer, IMediator mediator, ILogger<TimesheetDecisionPuller> logger)
    {
        _finalizer = finalizer;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default)
    {
        var changed = false;
        foreach (var week in weeks.Where(w => w.Status == TimesheetWeekStatus.Submitted || TimesheetFinalizer.NeedsTotals(w)))
        {
            try
            {
                if (!await _finalizer.HasPendingOutcomeAsync(week, ct))
                {
                    continue;
                }

                var response = await _mediator.Send(new FinalizeTimesheetDecisionCommand(week.Id, correlationId), ct);
                changed |= response.IsSuccessful;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // F14 — a READ never fails because a finalization did: a concurrent finalizer that won, a store error, a
                // totals conflict. This week is logged and skipped (the next read, the approvals page or the sweep takes
                // it again), and the caller re-reads, because the week may have moved part of the way.
                _logger.LogWarning(ex, "Finalizing timesheet week {WeekId} failed on a read path; skipped, will be retried.", week.Id);
                changed = true;
            }
        }

        return changed;
    }
}
