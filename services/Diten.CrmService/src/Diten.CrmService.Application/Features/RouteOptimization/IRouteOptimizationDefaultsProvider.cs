namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 — the configured placeholder values a route optimization is run with when the request omits them
/// (D-WORKINGHOURS-SOURCE = config, D-SPEED = config-with-default). An interface rather than constants because the pack
/// forbids magic numbers in the engine: the 09:00–18:00 field day, the lunch break, the road factor and the assumed
/// speed are OPERATIONAL settings, and moving any of them must be an ops change rather than a code change (the FU06B
/// <c>ICycleCapacityDefaultsProvider</c> precedent — the Application layer consumes the interface, Infrastructure holds
/// <c>IConfiguration</c>).
/// <para>These are <b>NOT</b> derived from <c>CycleCapacity</c>: <c>DailyWorkMinutes</c> is a minutes-per-day figure
/// with no start/end/lunch structure, so it cannot supply the slotting window. HR / MOD-0288 is the additive future
/// source; there is no HR integration in v1 (D-WORKINGHOURS).</para>
/// </summary>
public interface IRouteOptimizationDefaultsProvider
{
    RouteOptimizationDefaultsSet Current { get; }
}

/// <summary>The resolved defaults set — a per-day working window plus the travel-model constants.</summary>
public sealed record RouteOptimizationDefaultsSet(
    WorkingDayHours WorkingDay,
    double RoadFactor,
    double AssumedSpeedKmPerMin);

/// <summary>
/// The documented default constants, stated once (the pack's numbers). Used by the config provider as a fallback and by
/// <see cref="HaversineTravelModel"/> to guard a nonsensical override. Kept here, in the Application feature, so the
/// engine never hardcodes a bare literal.
/// </summary>
public static class RouteOptimizationDefaults
{
    public const string WorkDayStart = "09:00";
    public const string WorkDayEnd = "18:00";
    public const string LunchStart = "13:00";
    public const string LunchEnd = "14:00";

    /// <summary>Great-circle → road-distance correction. The pack's v1 number.</summary>
    public const double RoadFactor = 1.3;

    /// <summary>~40 km/h field average = 0.6667 km per minute. A sensible default, overridable via config (D-SPEED).</summary>
    public const double AssumedSpeedKmPerMin = 40.0 / 60.0;

    public static WorkingDayHours WorkingDay => new(WorkDayStart, WorkDayEnd, LunchStart, LunchEnd);

    public static RouteOptimizationDefaultsSet Set => new(WorkingDay, RoadFactor, AssumedSpeedKmPerMin);
}
