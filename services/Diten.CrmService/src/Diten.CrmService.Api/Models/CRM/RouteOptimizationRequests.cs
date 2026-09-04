using Diten.CrmService.Application.Features.RouteOptimization;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU03 dry-run preview request bodies — the wire shape of the <see cref="RouteOptimizationInput"/> contract
/// (pack §4.2 / §11). <c>TenantId</c> appears in none of them: this is a pure calculator over a supplied set and
/// persists nothing, so there is no tenant-scoped record to isolate. Times are <c>"HH:mm"</c> strings and dates are
/// <see cref="DateOnly"/> — never <c>DateTimeOffset</c>.
/// </summary>
public sealed class RouteOptimizationPreviewRequest
{
    public List<RouteVisitRequest> Visits { get; set; } = new();
    public RepWorkingHoursRequest? RepWorkingHours { get; set; }
    public OptimizationPeriodRequest? Period { get; set; }

    /// <summary>Buffer inserted BETWEEN consecutive visits (from CycleCapacity.BetweenVisitTimeMinutes, FU06B). 0–240.</summary>
    public int BetweenVisitMinutes { get; set; }

    public TravelModelSpecRequest? TravelModel { get; set; }

    public RouteOptimizationInput ToInput()
        => new(
            Visits.Select(v => v.ToInput()).ToList(),
            RepWorkingHours?.ToInput() ?? new RepWorkingHours(),
            Period?.ToInput() ?? new OptimizationPeriod(default, default),
            BetweenVisitMinutes,
            TravelModel?.ToSpec() ?? new TravelModelSpec());
}

public sealed class RouteVisitRequest
{
    public Guid VisitId { get; set; }
    public double Lat { get; set; }
    public double Long { get; set; }
    public int DurationMinutes { get; set; }
    public List<AvailabilityWindowRequest>? AvailabilityWindows { get; set; }
    public Guid? TargetId { get; set; }

    public RouteVisitInput ToInput()
        => new(
            VisitId, Lat, Long, DurationMinutes,
            AvailabilityWindows?.Select(w => w.ToWindow()).ToList(),
            TargetId);
}

public sealed class AvailabilityWindowRequest
{
    /// <summary>A WEEKDAY (monday…sunday), matching MOD-0150 ContactAvailability.</summary>
    public string Day { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;

    public AvailabilityWindow ToWindow() => new(Day, Start, End);
}

public sealed class RepWorkingHoursRequest
{
    public WorkingDayHoursRequest? PerDay { get; set; }
    public GeoPointRequest? StartLocation { get; set; }

    public RepWorkingHours ToInput() => new(PerDay?.ToInput(), StartLocation?.ToInput());
}

public sealed class WorkingDayHoursRequest
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string LunchStart { get; set; } = string.Empty;
    public string LunchEnd { get; set; } = string.Empty;

    public WorkingDayHours ToInput() => new(Start, End, LunchStart, LunchEnd);
}

public sealed class GeoPointRequest
{
    public double Lat { get; set; }
    public double Long { get; set; }

    public GeoPoint ToInput() => new(Lat, Long);
}

public sealed class OptimizationPeriodRequest
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }

    public OptimizationPeriod ToInput() => new(DateFrom, DateTo);
}

public sealed class TravelModelSpecRequest
{
    public string? Kind { get; set; }
    public double? RoadFactor { get; set; }
    public double? AssumedSpeedKmPerMin { get; set; }

    public TravelModelSpec ToSpec()
        => new(Kind ?? TravelModelKinds.Haversine, RoadFactor, AssumedSpeedKmPerMin);
}
