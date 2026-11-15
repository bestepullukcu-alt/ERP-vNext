using System.Globalization;

namespace Diten.InventoryService.Domain.WavePlanning.Rules;

/// <summary>What the conversion of a local cutoff said (pack §8.4).</summary>
public enum WaveCutoffOutcome
{
    Converted,

    /// <summary>The text is not <c>yyyy-MM-ddTHH:mm</c>.</summary>
    Malformed,

    /// <summary>The time zone id is not one the runtime's database knows.</summary>
    UnknownTimeZone,

    /// <summary>The local time does not exist in that zone (the daylight-saving gap).</summary>
    InGap
}

public sealed record WaveCutoff(WaveCutoffOutcome Outcome, DateTimeOffset AtUtc, bool AmbiguousResolved)
{
    public static WaveCutoff Failed(WaveCutoffOutcome outcome) => new(outcome, default, false);
}

/// <summary>
/// The cutoff in the WAREHOUSE's time zone (pack §8.4, D-3) — pure: the zone is always the one named, never the server's. A local time in
/// the daylight-saving gap does not exist (400 <c>WAVE_CUTOFF_INVALID</c>); a repeated local time takes the EARLIER instant.
/// </summary>
public static class WaveCutoffRules
{
    public const string LocalFormat = "yyyy-MM-dd'T'HH:mm";

    public static bool TryParseLocal(string? text, out DateTime local)
        => DateTime.TryParseExact(text, LocalFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    /// <summary>The time zone of an IANA id, or null when the runtime does not know it.</summary>
    public static TimeZoneInfo? Zone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>An IANA id the runtime resolves (<c>Europe/Istanbul</c>) — not a Windows name, not an offset.</summary>
    public static bool IsKnownIanaZone(string? timeZoneId)
        => timeZoneId is not null && timeZoneId.Contains('/', StringComparison.Ordinal) && timeZoneId.Trim() == timeZoneId && Zone(timeZoneId) is not null;

    public static WaveCutoff ToUtc(string? cutoffLocal, string? timeZoneId)
    {
        if (!TryParseLocal(cutoffLocal, out var local)) return WaveCutoff.Failed(WaveCutoffOutcome.Malformed);
        if (Zone(timeZoneId) is not { } zone) return WaveCutoff.Failed(WaveCutoffOutcome.UnknownTimeZone);

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified)) return WaveCutoff.Failed(WaveCutoffOutcome.InGap);
        if (zone.IsAmbiguousTime(unspecified))
        {
            // the hour is lived twice: the earlier instant is the one with the larger offset (daylight time)
            var earlier = zone.GetAmbiguousTimeOffsets(unspecified).Max();
            return new WaveCutoff(WaveCutoffOutcome.Converted, new DateTimeOffset(unspecified, earlier).ToUniversalTime(), true);
        }

        return new WaveCutoff(WaveCutoffOutcome.Converted, new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)).ToUniversalTime(), false);
    }

    /// <summary>Pack §8.4: an entered cutoff may be at most one hour in the past.</summary>
    public static bool IsTooOld(DateTimeOffset cutoffAtUtc, DateTimeOffset now) => cutoffAtUtc < now - WaveRules.CutoffPastTolerance;
}
