using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// WP-VW-W2 — one rep's days over a date range: the day kind (working / half / holiday / weekend), whether the day lies
/// in one of the rep's cycle periods, its visiting budget and how much of it the rep's written visits already take. The
/// SAME answer feeds the workspace calendar's <c>days[]</c>, the reschedule options and the reschedule date rule, so the
/// list the rep picks from and the rule that checks the pick cannot disagree.
/// <para><b>Sources (no new calculation):</b> the period = a non-archived planning session of the rep whose cycle period
/// covers the day; the day kind = <see cref="PlanningWorkingCalendar"/> for that period (4E — platform working calendar,
/// Sat/Sun fallback); the budget = <see cref="PlanningDayBudget"/> of the period's cycle capacity (4G); the load = the
/// rep's written, not cancelled / archived visits of the day (duration + between-visit buffer). A day in no period is
/// classified by the Sat/Sun rule with the default budget and <c>InPeriod = false</c>.</para>
/// </summary>
public sealed class VisitWorkspaceDays
{
    private readonly ITenantContext _tenant;
    private readonly IPlanningSessionRepository _sessions;
    private readonly ICyclePeriodReader _periods;
    private readonly ICycleCapacityRepository _capacities;
    private readonly PlanningWorkingCalendar _calendar;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IRouteOptimizationDefaultsProvider? _routeDefaults;

    public VisitWorkspaceDays(
        ITenantContext tenant,
        IPlanningSessionRepository sessions,
        ICyclePeriodReader periods,
        ICycleCapacityRepository capacities,
        PlanningWorkingCalendar calendar,
        IPlannedVisitRepository plannedVisits,
        IRouteOptimizationDefaultsProvider? routeDefaults = null)
    {
        _tenant = tenant;
        _sessions = sessions;
        _periods = periods;
        _capacities = capacities;
        _calendar = calendar;
        _plannedVisits = plannedVisits;
        _routeDefaults = routeDefaults;
    }

    /// <summary>Every day of [<paramref name="from"/>, <paramref name="to"/>] for <paramref name="resourceId"/>.</summary>
    public async Task<IReadOnlyList<VisitWorkspaceDay>> ReadAsync(
        string resourceId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || to < from)
        {
            return Array.Empty<VisitWorkspaceDay>();
        }

        var routeDay = _routeDefaults?.Current.WorkingDay ?? RouteOptimizationDefaults.WorkingDay;

        // The rep's periods (one per non-archived session), newest session first so an overlap resolves the same way.
        var sessions = (await _sessions.ListAsync(tenantId, cancellationToken))
            .Where(s => string.Equals(s.ResourceId, resourceId, StringComparison.Ordinal)
                        && !string.Equals(s.Status, PlanningSessionStatus.Archived, StringComparison.Ordinal))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToList();

        var frames = new List<PeriodFrame>();
        foreach (var periodId in sessions.Select(s => s.CyclePeriodId).Distinct())
        {
            var period = await _periods.GetByIdAsync(periodId, cancellationToken);
            if (period is null)
            {
                continue;
            }

            var start = DateOnly.FromDateTime(period.StartDate.UtcDateTime);
            var end = DateOnly.FromDateTime(period.EndDate.UtcDateTime);
            var clipFrom = start > from ? start : from;
            var clipTo = end < to ? end : to;
            if (clipTo < clipFrom)
            {
                continue;
            }

            var capacity = await _capacities.GetByCyclePeriodAsync(tenantId, periodId, cancellationToken);
            var calendar = await _calendar.ResolveAsync(period, capacity?.CalendarCountryCode, clipFrom, clipTo, cancellationToken);
            frames.Add(new PeriodFrame(periodId, clipFrom, clipTo, calendar, PlanningDayBudget.From(capacity, routeDay),
                capacity?.BetweenVisitTimeMinutes ?? 0, VisitPlanningEngine.DefaultDuration(capacity)));
        }

        var outside = PlanningWorkingCalendar.WeekendFallback(from, to, "no_period", "No cycle period covers the day.");
        var defaultBudget = PlanningDayBudget.From(null, routeDay);
        var defaultDuration = VisitPlanningEngine.DefaultDuration(null);

        var load = (await _plannedVisits.ListAsync(tenantId, cancellationToken))
            .Where(v => string.Equals(v.Resource.ResourceId, resourceId, StringComparison.Ordinal)
                        && v.PlannedDate >= from && v.PlannedDate <= to
                        && !v.IsCancelled() && !v.IsArchived())
            .GroupBy(v => v.PlannedDate)
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<VisitWorkspaceDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var frame = frames.FirstOrDefault(f => day >= f.From && day <= f.To);
            var kind = frame?.Calendar.KindOf(day) ?? outside.KindOf(day);
            var budget = frame?.Budget ?? defaultBudget;
            var buffer = frame?.BufferMinutes ?? 0;
            var typical = frame?.TypicalMinutes ?? defaultDuration;
            var visits = load.GetValueOrDefault(day) ?? new List<Domain.Entities.PlannedVisit>();
            var planned = visits.Sum(v => (v.PlannedDurationMinutes ?? typical) + buffer);
            days.Add(new VisitWorkspaceDay(
                day, kind, frame is not null, frame?.CyclePeriodId, budget.BudgetFor(kind), planned, visits.Count));
        }

        return days;
    }

    /// <summary>The reschedule date rule (A2): after today, a working day, inside one of the rep's periods.</summary>
    public async Task<bool> CanRescheduleToAsync(
        string resourceId, DateOnly date, DateOnly today, CancellationToken cancellationToken)
    {
        if (date <= today)
        {
            return false;
        }

        var day = (await ReadAsync(resourceId, date, date, cancellationToken)).SingleOrDefault();
        return day?.CanTakeVisit == true;
    }

    /// <summary>The next <paramref name="count"/> days a visit can be moved to (after today), read in 4-week chunks up to
    /// 12 weeks ahead. Holidays / weekends / days outside a period are skipped.</summary>
    public async Task<IReadOnlyList<VisitWorkspaceDay>> RescheduleOptionsAsync(
        string resourceId, DateOnly today, int count, CancellationToken cancellationToken)
    {
        var result = new List<VisitWorkspaceDay>();
        var from = today.AddDays(1);
        for (var chunk = 0; chunk < 3 && result.Count < count; chunk++)
        {
            var to = from.AddDays(27);
            var days = await ReadAsync(resourceId, from, to, cancellationToken);
            result.AddRange(days.Where(d => d.CanTakeVisit).Take(count - result.Count));
            from = to.AddDays(1);
        }

        return result;
    }

    private sealed record PeriodFrame(
        Guid CyclePeriodId, DateOnly From, DateOnly To, PlanningCalendarResult Calendar, PlanningDayBudget Budget,
        int BufferMinutes, int TypicalMinutes);
}

/// <summary>One day of a rep (see <see cref="VisitWorkspaceDays"/>).</summary>
public sealed record VisitWorkspaceDay(
    DateOnly Date,
    string Kind,
    bool InPeriod,
    Guid? CyclePeriodId,
    int CapacityMinutes,
    int PlannedMinutes,
    int PlannedCount)
{
    public bool IsWorking => Kind is PlanningDayKinds.Working or PlanningDayKinds.Half;

    public bool IsHoliday => Kind == PlanningDayKinds.Holiday;

    /// <summary>A day a visit can be moved to: a working (or half) day inside an active period.</summary>
    public bool CanTakeVisit => IsWorking && InPeriod;
}
