namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 route + time-window scheduler seam. The SINGLE source of truth for how a GIVEN visit set is ordered
/// and slotted across a period. v1 = the in-process greedy time-window insertion heuristic; a real solver (OR-Tools /
/// VROOM) can swap behind THIS interface with no contract change (F-SOLVER). Consumers — the FU05 packing engine
/// in-process and the FU03 dry-run preview endpoint's handler — call THIS; no consumer re-implements the heuristic, and
/// there is no HTTP self-call back through the Gateway. The optimizer performs NO writes and reads NO repository.
/// <para>Synchronous and over a fully-materialised input: unlike the resolver seams it needs no repository / tenant
/// load, so the caller supplies everything and the engine stays pure and trivially testable. A future async solver
/// would make the seam <c>Task&lt;...&gt; OptimizeAsync(...)</c> — F-SOLVER, not assumed here.</para>
/// </summary>
public interface IRouteOptimizer
{
    RouteOptimizationOutput Optimize(RouteOptimizationInput input);
}

/// <summary>
/// v1 implementation — a thin adapter that resolves the config placeholders (working hours, road factor, assumed speed)
/// the request omits, builds the pure travel model, and delegates to the pure <see cref="TimeWindowInsertionEngine"/>.
/// It holds NO repository and does NO I/O of its own; the only injected dependency is the config defaults provider.
/// </summary>
public sealed class GreedyTimeWindowRouteOptimizer : IRouteOptimizer
{
    private readonly IRouteOptimizationDefaultsProvider _defaults;

    public GreedyTimeWindowRouteOptimizer(IRouteOptimizationDefaultsProvider defaults)
    {
        _defaults = defaults;
    }

    public RouteOptimizationOutput Optimize(RouteOptimizationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var defaults = _defaults.Current;

        // Working hours: the request's per-day window, else the configured placeholder (D-WORKINGHOURS-SOURCE = config).
        var workingDay = input.RepWorkingHours?.PerDay ?? defaults.WorkingDay;

        // Travel model: request spec overrides win, else the configured defaults (D-SPEED = config-with-default). No
        // external routing/map/geocoding API is ever constructed here (D-TRAVEL).
        var spec = input.TravelModel ?? new TravelModelSpec();
        var travel = new HaversineTravelModel(
            spec.RoadFactor ?? defaults.RoadFactor,
            spec.AssumedSpeedKmPerMin ?? defaults.AssumedSpeedKmPerMin);

        // A supplied manual sequence switches to the ordered scheduler (same feasibility, caller's order); null/empty is
        // the greedy default, unchanged.
        if (input.OrderedTargetIds is { Count: > 0 } ordered)
        {
            return TimeWindowInsertionEngine.ScheduleInOrder(
                input.Visits ?? Array.Empty<RouteVisitInput>(),
                workingDay,
                input.RepWorkingHours?.StartLocation,
                input.Period,
                input.BetweenVisitMinutes,
                travel,
                ordered);
        }

        return TimeWindowInsertionEngine.Schedule(
            input.Visits ?? Array.Empty<RouteVisitInput>(),
            workingDay,
            input.RepWorkingHours?.StartLocation,
            input.Period,
            input.BetweenVisitMinutes,
            travel);
    }
}
