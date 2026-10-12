using Diten.CrmService.Application.Features.RouteOptimization;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3B (MK-8, MK-9) — how many minutes of visiting one day holds, and how many visits that is.
/// <list type="bullet">
/// <item><b>Working minutes of a day</b> (<see cref="DailyWorkMinutes"/>) — the pinned <c>CycleCapacity.DailyWorkMinutes</c>;
/// without a capacity, the configured route day (09:00–18:00) minus its lunch (13:00–14:00) = 480.</item>
/// <item><b>Daily visit budget</b> (<see cref="BudgetMinutes"/>) = working minutes − <c>CycleCapacity.DailyFixedMinutes()</c>
/// (travel + report + quiz charged per field day; 0 without a capacity). The budget bounds Σ(visit duration + between-visit
/// buffer) of a day.</item>
/// <item><b>Day kinds</b> — <c>working</c>: the budget; <c>half</c>: half of it (rounded down); <c>holiday</c> /
/// <c>weekend</c>: 0.</item>
/// <item><b>Daily cap</b> (<see cref="DailyCap"/>, information) = ⌊budget ÷ (typical visit minutes + buffer)⌋ — the
/// mockup's "at most N a day"; a half day ⌊(budget ÷ 2) ÷ (…)⌋.</item>
/// <item><b>Route window</b> — the day starts at the configured start (09:00); it ends when the day's working minutes
/// are spent, the lunch break excluded exactly as today (the end moves past lunch by the lunch length when the working
/// minutes run through it). Without a capacity this is today's 09:00–18:00.</item>
/// </list>
/// Pure: a function of the capacity and the configured route day.
/// </summary>
public sealed record PlanningDayBudget(
    int DailyWorkMinutes,
    int DailyFixedMinutes,
    int BudgetMinutes,
    int BufferMinutes,
    int TypicalVisitMinutes,
    int DailyCap,
    string Source)
{
    public const string FromCycleCapacity = "cycle_capacity";
    public const string FromDefaultHours = "default_hours";

    public static PlanningDayBudget From(CapacityEntity? capacity, WorkingDayHours routeDay)
    {
        var start = RouteTime.ParseMinutes(routeDay.Start) ?? 9 * 60;
        var end = RouteTime.ParseMinutes(routeDay.End) ?? 18 * 60;
        var lunch = LunchMinutes(routeDay);
        var defaultWork = Math.Max(0, end - start - lunch);

        var work = capacity is { DailyWorkMinutes: > 0 } ? capacity.DailyWorkMinutes : defaultWork;
        var fixedMinutes = capacity is null ? 0 : Math.Max(0, capacity.DailyFixedMinutes());
        var budget = Math.Max(0, work - fixedMinutes);
        var buffer = Math.Max(0, capacity?.BetweenVisitTimeMinutes ?? 0);
        var typical = VisitPlanningEngine.DefaultDuration(capacity);
        var unit = Math.Max(1, typical + buffer);

        return new PlanningDayBudget(
            work, fixedMinutes, budget, buffer, typical, budget / unit,
            capacity is null ? FromDefaultHours : FromCycleCapacity);
    }

    /// <summary>The visiting minutes a day of this kind holds.</summary>
    public int BudgetFor(string kind) => kind switch
    {
        PlanningDayKinds.Working => BudgetMinutes,
        PlanningDayKinds.Half => BudgetMinutes / 2,
        _ => 0
    };

    /// <summary>The information cap ("at most N a day") for a day of this kind.</summary>
    public int CapFor(string kind) => BudgetFor(kind) / Math.Max(1, TypicalVisitMinutes + BufferMinutes);

    /// <summary>The route optimizer's working window for a day of this kind (09:00 + the day's working minutes, lunch
    /// excluded as today). A non-working kind gets the working day's window (it holds no visit anyway).</summary>
    public WorkingDayHours WindowFor(string kind, WorkingDayHours routeDay)
    {
        var start = RouteTime.ParseMinutes(routeDay.Start) ?? 9 * 60;
        var lunchStart = RouteTime.ParseMinutes(routeDay.LunchStart) ?? 13 * 60;
        var lunch = LunchMinutes(routeDay);
        var minutes = kind == PlanningDayKinds.Half ? DailyWorkMinutes / 2 : DailyWorkMinutes;
        var end = start + minutes;
        if (lunch > 0 && end > lunchStart && start < lunchStart)
        {
            end += lunch;
        }

        end = Math.Min(end, 24 * 60 - 1);
        return new WorkingDayHours(RouteTime.Format(start), RouteTime.Format(end), routeDay.LunchStart, routeDay.LunchEnd);
    }

    private static int LunchMinutes(WorkingDayHours routeDay)
    {
        var lunchStart = RouteTime.ParseMinutes(routeDay.LunchStart);
        var lunchEnd = RouteTime.ParseMinutes(routeDay.LunchEnd);
        return lunchStart is { } ls && lunchEnd is { } le && le > ls ? le - ls : 0;
    }
}
