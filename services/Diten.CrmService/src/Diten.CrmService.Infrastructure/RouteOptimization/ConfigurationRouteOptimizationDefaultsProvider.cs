using Diten.CrmService.Application.Features.RouteOptimization;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 — reads the route-optimization placeholders from configuration, so the 09:00–18:00 field day, the
/// lunch break, the road factor and the assumed field speed are OPERATIONAL settings rather than constants compiled
/// into the engine (D-WORKINGHOURS-SOURCE = config, D-SPEED = config-with-default). The FU06B
/// <c>ConfigurationCycleCapacityDefaultsProvider</c> precedent: Infrastructure holds <c>IConfiguration</c>, the
/// Application engine consumes only the interface.
/// <para>A missing or nonsensical setting falls back to the documented default rather than throwing — a scheduler that
/// refuses to start because an optional number is absent is worse than one that starts with the pack's defaults.</para>
/// </summary>
public sealed class ConfigurationRouteOptimizationDefaultsProvider : IRouteOptimizationDefaultsProvider
{
    private readonly RouteOptimizationDefaultsSet _defaults;

    public ConfigurationRouteOptimizationDefaultsProvider(IConfiguration configuration)
    {
        var start = Valid(configuration["RouteOptimization:WorkDayStart"]) ?? RouteOptimizationDefaults.WorkDayStart;
        var end = Valid(configuration["RouteOptimization:WorkDayEnd"]) ?? RouteOptimizationDefaults.WorkDayEnd;
        var lunchStart = Valid(configuration["RouteOptimization:LunchStart"]) ?? RouteOptimizationDefaults.LunchStart;
        var lunchEnd = Valid(configuration["RouteOptimization:LunchEnd"]) ?? RouteOptimizationDefaults.LunchEnd;

        var roadFactor = configuration.GetValue<double?>("RouteOptimization:RoadFactor");
        var speed = configuration.GetValue<double?>("RouteOptimization:AssumedSpeedKmPerMin");

        _defaults = new RouteOptimizationDefaultsSet(
            new WorkingDayHours(start, end, lunchStart, lunchEnd),
            roadFactor is > 0 and <= 10 ? roadFactor.Value : RouteOptimizationDefaults.RoadFactor,
            speed is > 0 and <= 10 ? speed.Value : RouteOptimizationDefaults.AssumedSpeedKmPerMin);
    }

    /// <summary>Accepts a config value only when it parses as "HH:mm"; otherwise the caller falls back to the default.</summary>
    private static string? Valid(string? value)
        => RouteTimeParse(value) ? value : null;

    private static bool RouteTimeParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split(':');
        return parts.Length >= 2
               && int.TryParse(parts[0], out var h)
               && int.TryParse(parts[1], out var m)
               && h is >= 0 and <= 23
               && m is >= 0 and <= 59;
    }

    public RouteOptimizationDefaultsSet Current => _defaults;
}
