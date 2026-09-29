using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

public interface ITimesheetDecisionPuller
{
    /// <summary>Finalizes every submitted week in <paramref name="weeks"/> whose MOD-0023 outcome is waiting. True when
    /// at least one was finalized (the caller re-reads).</summary>
    Task<bool> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default);
}

/// <summary>
/// The read-time door to the finalizer (D7: "it runs on week read, on the approvals page and in the sweep job"). It
/// only SENDS the audited <see cref="FinalizeTimesheetDecisionCommand"/> when an outcome is actually waiting, so a
/// page load that finds nothing to do writes no audit entry.
/// </summary>
public sealed class TimesheetDecisionPuller : ITimesheetDecisionPuller
{
    private readonly ITimesheetFinalizer _finalizer;
    private readonly IMediator _mediator;

    public TimesheetDecisionPuller(ITimesheetFinalizer finalizer, IMediator mediator)
    {
        _finalizer = finalizer;
        _mediator = mediator;
    }

    public async Task<bool> PullAsync(IEnumerable<TimesheetWeek> weeks, string correlationId, CancellationToken ct = default)
    {
        var changed = false;
        foreach (var week in weeks.Where(w => w.Status == TimesheetWeekStatus.Submitted))
        {
            if (!await _finalizer.HasPendingOutcomeAsync(week, ct))
            {
                continue;
            }

            var response = await _mediator.Send(new FinalizeTimesheetDecisionCommand(week.Id, correlationId), ct);
            changed |= response.IsSuccessful;
        }

        return changed;
    }
}
