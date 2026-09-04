namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 travel-cost seam. Returns the travel time in minutes between two points. A pure seam so a real
/// distance matrix (or a solver-provided matrix) can swap behind it later with no engine change (F-SOLVER). v1 =
/// <see cref="HaversineTravelModel"/>; there is NO external routing/map/geocoding API (D-TRAVEL — pharma HCP locations
/// must not leave the system, and cost/rate-limits are avoided).
/// </summary>
public interface ITravelModel
{
    /// <summary>Travel minutes from <paramref name="from"/> to <paramref name="to"/> (non-negative).</summary>
    double TravelMinutes(GeoPoint from, GeoPoint to);
}

/// <summary>
/// In-house great-circle travel model: <c>haversineKm × roadFactor / assumedSpeedKmPerMin</c>. <b>Pure</b> — no I/O,
/// no <c>HttpClient</c>, no clock, immutable after construction. <see cref="RoadFactor"/> corrects great-circle to
/// road distance; both it and <see cref="AssumedSpeedKmPerMin"/> are supplied by the caller (from the request spec or
/// the config defaults provider) — never a magic number baked into the algorithm (D-SPEED = config-with-default).
/// </summary>
public sealed class HaversineTravelModel : ITravelModel
{
    private const double EarthRadiusKm = 6371.0088;

    public double RoadFactor { get; }
    public double AssumedSpeedKmPerMin { get; }

    public HaversineTravelModel(double roadFactor, double assumedSpeedKmPerMin)
    {
        // Guard against a nonsensical config that would make every trip free or infinite. Fall back to sane, documented
        // constants rather than throwing: the model is a scheduling input, not a correctness gate.
        RoadFactor = roadFactor > 0 ? roadFactor : RouteOptimizationDefaults.RoadFactor;
        AssumedSpeedKmPerMin = assumedSpeedKmPerMin > 0
            ? assumedSpeedKmPerMin
            : RouteOptimizationDefaults.AssumedSpeedKmPerMin;
    }

    public double TravelMinutes(GeoPoint from, GeoPoint to)
    {
        var km = HaversineKm(from, to);
        return km * RoadFactor / AssumedSpeedKmPerMin;
    }

    /// <summary>Great-circle distance in kilometres between two lat/long points.</summary>
    public static double HaversineKm(GeoPoint from, GeoPoint to)
    {
        var lat1 = DegreesToRadians(from.Lat);
        var lat2 = DegreesToRadians(to.Lat);
        var dLat = DegreesToRadians(to.Lat - from.Lat);
        var dLon = DegreesToRadians(to.Long - from.Long);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
