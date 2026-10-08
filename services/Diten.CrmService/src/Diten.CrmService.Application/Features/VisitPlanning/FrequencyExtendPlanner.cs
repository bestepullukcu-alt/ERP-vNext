using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 frequency step, re-ruled by WP-VP-3A (B1, B2, MK-7, K-2). For each target it asks MOD-0165
/// <see cref="IVisitFrequencyPolicyResolver"/> (READ-ONLY, unchanged) for the cadence and turns it into "how many visits
/// does the WHOLE PERIOD need" (<see cref="ResolveRequirementAsync"/>); the engine then spreads what is still missing over
/// the period's draft weeks (<see cref="Distribute"/>).
/// <para><b>Visits needed in the period</b> = the policy's <c>RequiredVisitCount</c> × the period's units of its
/// <c>PeriodType</c> (<see cref="UnitsIn"/>). An unknown cadence (no policy) is <b>1 visit in the period</b> with
/// <c>frequencyStatus = unknown</c> — visible, never silently dropped and never invented beyond one. A pharmacy / bare
/// account is resolved as an <c>account</c> target (a policy aimed at the account), else the same "1 + unknown".</para>
/// </summary>
public sealed class FrequencyExtendPlanner
{
    private readonly IVisitFrequencyPolicyResolver _frequency;

    public FrequencyExtendPlanner(IVisitFrequencyPolicyResolver frequency) => _frequency = frequency;

    /// <summary>How many visits the target needs in the period, and the cadence it came from.</summary>
    public async Task<FrequencyRequirement> ResolveRequirementAsync(
        string targetType, Guid targetId, DateTimeOffset effectiveAt, PlanningPeriodFrame period,
        CancellationToken cancellationToken)
    {
        if (targetId == Guid.Empty)
        {
            return FrequencyRequirement.Unknown;
        }

        // A pharmacy / bare account carries no policy of its own type: the account policy (if any) is its cadence.
        var resolveAs = string.Equals(targetType, PlannedVisitTargetType.Contact, StringComparison.Ordinal)
            ? targetType
            : FrequencyTargetType.Account;
        var result = await _frequency.ResolveAsync(
            new ResolveVisitFrequencyPolicyQuery(TargetType: resolveAs, TargetId: targetId, EffectiveAt: effectiveAt),
            cancellationToken);

        if (result.RequiredVisitCount is not { } count || count <= 0
            || string.Equals(result.FrequencyStatus, FrequencyStatus.Unknown, StringComparison.Ordinal))
        {
            return FrequencyRequirement.Unknown;
        }

        return new FrequencyRequirement(
            result.FrequencyStatus, count, result.PeriodType, count * UnitsIn(result.PeriodType, period));
    }

    /// <summary>
    /// The period's units of a <c>PeriodType</c>:
    /// <list type="bullet">
    /// <item><c>week</c> → working weeks (a Monday-week with at least one working day inside the period);</item>
    /// <item><c>month</c> → calendar months the period touches (a partial month counts as a whole one, never pro rata);</item>
    /// <item><c>quarter</c> → calendar quarters the period touches (same rule);</item>
    /// <item><c>day</c> → working days in the period;</item>
    /// <item><c>cycle</c> / <c>campaign-period</c> / <c>custom</c> / anything unknown → 1 (the period itself).</item>
    /// </list>
    /// Never less than 1.
    /// </summary>
    public static int UnitsIn(string? periodType, PlanningPeriodFrame period)
        => Math.Max(1, (periodType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            FrequencyPeriodType.Week => period.WorkingWeeks,
            FrequencyPeriodType.Month => period.Months,
            FrequencyPeriodType.Quarter => period.Quarters,
            FrequencyPeriodType.Day => period.WorkingDays,
            _ => 1
        });

    /// <summary>
    /// Spreads <paramref name="remaining"/> visits over <paramref name="weekCount"/> draft weeks at an even stride: the
    /// k-th visit (k = 0…n−1) goes to draft week ⌊k · weekCount / remaining⌋. So 2 visits over 13 weeks land in weeks 0 and
    /// 6 (6–7 weeks apart), the first visit is always in the first draft week, and a week gets a second visit of the same
    /// target ONLY when remaining &gt; weekCount. Returned indices are into the draft-week list, ascending.
    /// </summary>
    public static IReadOnlyList<int> Distribute(int remaining, int weekCount)
    {
        if (remaining <= 0 || weekCount <= 0)
        {
            return Array.Empty<int>();
        }

        var weeks = new List<int>(remaining);
        for (var k = 0; k < remaining; k++)
        {
            weeks.Add((int)((long)k * weekCount / remaining));
        }

        return weeks;
    }
}

/// <summary>WP-VP-3A — the visits a target needs in the period. <see cref="RequiredInPeriod"/> is what the engine plans
/// toward; <see cref="PolicyVisitCount"/> / <see cref="PeriodType"/> are the policy as authored (null when unknown).</summary>
public sealed record FrequencyRequirement(
    string FrequencyStatus,
    int? PolicyVisitCount,
    string? PeriodType,
    int RequiredInPeriod)
{
    public static readonly FrequencyRequirement Unknown =
        new(Features.VisitFrequencyPolicy.Resolve.FrequencyStatus.Unknown, null, null, 1);
}

/// <summary>WP-VP-3A — the period as the frequency rule counts it: its days, the non-working days (weekends + holidays
/// from the working calendar, or the Sat/Sun fallback) and the derived units.</summary>
public sealed record PlanningPeriodFrame(DateOnly Start, DateOnly End, IReadOnlyCollection<DateOnly> NonWorkingDates)
{
    private bool IsWorking(DateOnly day) => !NonWorkingDates.Contains(day);

    private IEnumerable<DateOnly> Days()
    {
        for (var d = Start; d <= End; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    public int WorkingDays => Days().Count(IsWorking);

    public int WorkingWeeks => PlanningWeekCalendar.PeriodWeeks(Start, End)
        .Count(w => Enumerable.Range(0, w.To.DayNumber - w.From.DayNumber + 1).Any(i => IsWorking(w.From.AddDays(i))));

    public int Months => Days().Select(d => (d.Year, d.Month)).Distinct().Count();

    public int Quarters => Days().Select(d => (d.Year, (d.Month - 1) / 3)).Distinct().Count();
}
