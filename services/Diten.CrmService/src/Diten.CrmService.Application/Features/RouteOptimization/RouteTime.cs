namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// Pure time / coordinate helpers for the FU03 engine. Self-contained (no dependency on the MOD-0150
/// <c>ContactAvailability</c> aggregate, which FU03 reads only as a reference and never couples to). Times are
/// <c>"HH:mm"</c> minutes-from-midnight; weekdays are the same lowercase vocabulary MOD-0150 uses.
/// </summary>
internal static class RouteTime
{
    internal static readonly string[] Weekdays =
        { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };

    /// <summary>Parses "HH:mm" (24h) into minutes from midnight; null when absent or malformed.</summary>
    internal static int? ParseMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Split(':');
        if (parts.Length < 2
            || !int.TryParse(parts[0], out var hours)
            || !int.TryParse(parts[1], out var minutes)
            || hours is < 0 or > 23
            || minutes is < 0 or > 59)
        {
            return null;
        }

        return (hours * 60) + minutes;
    }

    /// <summary>Formats minutes-from-midnight back to "HH:mm".</summary>
    internal static string Format(int minutes) => $"{minutes / 60:D2}:{minutes % 60:D2}";

    /// <summary>The lowercase weekday name of a calendar date (matches MOD-0150 ContactAvailability).</summary>
    internal static string WeekdayFromDate(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "monday",
        DayOfWeek.Tuesday => "tuesday",
        DayOfWeek.Wednesday => "wednesday",
        DayOfWeek.Thursday => "thursday",
        DayOfWeek.Friday => "friday",
        DayOfWeek.Saturday => "saturday",
        _ => "sunday"
    };

    /// <summary>A lat/long pair is usable only when both are finite and in range.</summary>
    internal static bool IsValidCoordinate(double lat, double lon)
        => !double.IsNaN(lat) && !double.IsNaN(lon)
           && !double.IsInfinity(lat) && !double.IsInfinity(lon)
           && lat is >= -90 and <= 90
           && lon is >= -180 and <= 180;
}
