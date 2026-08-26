using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// Operator-tunable SLA policy. The WINDOW is a policy, not a shape.
///
/// <para>It lives here rather than in the executable contract because the contract declares what the two sides
/// must AGREE on — the vocabulary of states — and a tenant tightening its warning window from two days to one is
/// not a change either side needs to be told about. Declaring the number in <c>fixture-contract.js</c> would have
/// frozen a business rule into a shape validator and made a policy change a frontend edit.</para>
///
/// <para>What it replaces: a literal <c>2</c> inside the browser's <c>computeSla</c>, of unrecorded origin, that
/// no tenant could change and no test could vary.</para>
/// </summary>
public sealed class WorkItemSlaOptions
{
    public const string SectionName = "WorkAggregation:Sla";

    /// <summary>
    /// How much working time before the deadline the warning window opens. The default reproduces the threshold
    /// the browser used, so this slice moves the decision without silently changing what anyone sees.
    /// </summary>
    public decimal DueSoonWithinWorkingDays { get; set; } = 2m;
}

/// <summary>
/// WC-2 — whether work is overdue, close to it, or comfortable. Decided HERE, on the server, from
/// <see cref="IWorkingTimeCalculator"/>.
///
/// <para><b>What this replaces.</b> The browser decided it: <c>mock-data.js computeSla()</c>, calendar-day
/// subtraction against a hard-coded <c>&lt;= 2</c>, with no notion of working time at all. That inverted the
/// surface's own law — the client renders what the projection states and never derives eligibility — and it left
/// WC-2 with nothing to point the working calendar at.</para>
///
/// <para><b>Every question goes through the seam.</b> Nothing here subtracts dates. The boundary comes from
/// <see cref="IWorkingTimeCalculator.Add"/> and the measure from
/// <see cref="IWorkingTimeCalculator.UnitsBetween"/>, so swapping the calculator swaps the answers — which is
/// what makes the seam real rather than decorative.</para>
/// </summary>
public interface IWorkItemSlaCalculator
{
    /// <summary>
    /// The contract's <c>slaState</c> for a deadline, or <c>no-sla</c> when there is none. Work without a
    /// deadline is a legitimate state, not a missing one.
    /// </summary>
    string Resolve(DateTimeOffset? dueAt, DateTimeOffset now);
}

public sealed class WorkItemSlaCalculator : IWorkItemSlaCalculator
{
    private readonly IWorkingTimeCalculator _workingTime;
    private readonly WorkItemSlaOptions _options;

    public WorkItemSlaCalculator(IWorkingTimeCalculator workingTime, IOptions<WorkItemSlaOptions> options)
    {
        _workingTime = workingTime;
        _options = options.Value;
    }

    public string Resolve(DateTimeOffset? dueAt, DateTimeOffset now)
    {
        if (dueAt is not { } deadline)
        {
            return WorkItemContract.SlaNoSla;
        }

        deadline = EndOfDeadlineDay(deadline);

        if (now > deadline)
        {
            return WorkItemContract.SlaOverdue;
        }

        /*
         * The warning window's opening instant, walked BACK from the deadline through working time — not derived
         * by comparing the remaining measure against the threshold.
         *
         * Under this slice's 24/7 calculator the two agree. Under a real one they do not, and Add is the correct
         * question: two working days before a Monday deadline is the preceding Thursday. Comparing a measure
         * would have opened the window on Saturday and quietly told everyone their Monday work was urgent all
         * weekend.
         */
        var windowOpensAt = _workingTime.Add(deadline, -_options.DueSoonWithinWorkingDays);

        return now >= windowOpensAt ? WorkItemContract.SlaDueSoon : WorkItemContract.SlaOnTrack;
    }

    /// <summary>
    /// A deadline that lands exactly on a date boundary is a DATE, and a date-only deadline means "by the end of
    /// that day".
    ///
    /// <para>Task due dates are entered as dates and stored at midnight. Comparing <c>now</c> to that instant
    /// directly would make every task overdue from 00:00 on the day it is due — the browser never did that
    /// (it compared whole days), so shipping it would have been a silent regression dressed as a move to the
    /// server. Anything carrying a real time of day is left exactly as given: that IS a deadline instant.</para>
    /// </summary>
    private static DateTimeOffset EndOfDeadlineDay(DateTimeOffset deadline)
        => deadline.TimeOfDay == TimeSpan.Zero ? deadline.AddDays(1) : deadline;
}
