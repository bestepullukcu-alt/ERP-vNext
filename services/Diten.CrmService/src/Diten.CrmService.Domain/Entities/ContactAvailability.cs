namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0150 FU07 — when a contact can be visited <b>at one specific account/location</b>. The owning key is
/// <see cref="AccountContactLinkId"/>, never the contact: the same doctor works at several hospitals/clinics/pharmacies
/// with different days and hours, so a flat field on <see cref="Contact"/> would collapse them into one wrong schedule
/// (pack D8). <see cref="ContactId"/> / <see cref="AccountId"/> are navigation copies <b>derived from the link</b> —
/// they are never taken from the request payload.
/// <para>
/// Times are local wall-clock times of the account location, stored as "HH:mm" strings; MOD-0150 owns no timezone
/// master, so consumers must not reinterpret them as instants. Closing is done with
/// <see cref="AvailabilityLifecycle"/> (inactive/archived) — there is no hard delete.
/// </para>
/// This is master data for MOD-0151 FU09A route readiness and MOD-0155 visit planning; it plans nothing itself.
/// </summary>
public sealed class ContactAvailability : EntityBase
{
    /// <summary>Owning AccountContactLink (MOD-0150 FU03). The single source of the contact/account pair.</summary>
    public Guid AccountContactLinkId { get; set; }

    /// <summary>Navigation copy of the link's ContactId (derived, never client-supplied).</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation copy of the link's AccountId (derived, never client-supplied).</summary>
    public Guid AccountId { get; set; }

    /// <summary>Stable machine-readable ISO weekday (<c>monday</c> … <c>sunday</c>) — not a localized label and not
    /// tenant vocabulary, so it is validated in-domain (<see cref="AvailabilityWeekday"/>) rather than via MOD-0048.</summary>
    public string Weekday { get; set; } = string.Empty;

    /// <summary>Available window start, "HH:mm" local wall-clock.</summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>Available window end, "HH:mm" local wall-clock. Must be after <see cref="StartTime"/>.</summary>
    public string EndTime { get; set; } = string.Empty;

    /// <summary>Visit preference for THIS link/location (preferred + avoid window, appointment rules, durations).
    /// Optional; when absent the available window is used (pack D11). <c>AppointmentRequired</c> and
    /// <c>AppointmentLeadTimeDays</c> live HERE only — they are surfaced at row level in the DTO but never stored
    /// twice, so the two copies can never drift apart.</summary>
    public VisitPreference Preference { get; set; } = new();

    /// <summary>Typical visit length at this location, in minutes. An availability fact (how long a visit takes here),
    /// not a preference — MOD-0155 uses it for slot sizing; MOD-0150 never plans with it.</summary>
    public int? AverageVisitDurationMinutes { get; set; }

    /// <summary>MOD-0048 <c>contact-availability-type</c> published value (working-hours, visiting-hours,
    /// preferred-window, restricted-window, appointment-only, temporary-exception).</summary>
    public string AvailabilityType { get; set; } = string.Empty;

    /// <summary>MOD-0048 <c>contact-availability-source</c> published value (how the row was captured).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>MOD-0048 <c>contact-availability-status</c> published value (active / inactive / archived).</summary>
    public string Status { get; set; } = AvailabilityLifecycle.Active;

    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// MOD-0150 FU07 visit preference (value object). Read in the AccountContactLink context (pack D10): a contact-level
/// default may exist elsewhere, but the link-scoped value always wins. <c>Avoid*</c> is NOT the inverse of
/// <c>Preferred*</c> — it is a stronger constraint inside the available window (pack D13).
/// </summary>
public sealed class VisitPreference
{
    public int? PreferredVisitDurationMinutes { get; set; }
    public string? PreferredVisitStartTime { get; set; }
    public string? PreferredVisitEndTime { get; set; }
    public string? AvoidVisitStartTime { get; set; }
    public string? AvoidVisitEndTime { get; set; }

    /// <summary>An appointment is required before visiting. This never drops a route candidate — it produces a
    /// warning/reason for MOD-0151 FU09A / MOD-0155 (pack D14).</summary>
    public bool AppointmentRequired { get; set; }

    public int? AppointmentLeadTimeDays { get; set; }

    /// <summary>Optional MOD-0048 <c>communication-preference-type</c> value; reuses the existing set (no new one).</summary>
    public string? PreferredContactMethod { get; set; }

    public string? Notes { get; set; }

    public bool HasPreferredWindow => !string.IsNullOrWhiteSpace(PreferredVisitStartTime) && !string.IsNullOrWhiteSpace(PreferredVisitEndTime);

    public bool HasAvoidWindow => !string.IsNullOrWhiteSpace(AvoidVisitStartTime) && !string.IsNullOrWhiteSpace(AvoidVisitEndTime);
}

/// <summary>Availability/exception lifecycle. Hard delete does not exist; a row is closed with inactive/archived.</summary>
public static class AvailabilityLifecycle
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Active, Inactive, Archived };

    /// <summary>Closed = no longer operative (never returned as current availability, still readable as history).</summary>
    public static bool IsClosed(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && !string.Equals(status.Trim(), Active, StringComparison.OrdinalIgnoreCase);
}

/// <summary>ISO weekday vocabulary + the wall-clock "HH:mm" helpers the availability rules run on.</summary>
public static class AvailabilityWeekday
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };

    public static bool IsValid(string? weekday)
        => !string.IsNullOrWhiteSpace(weekday) && All.Contains(weekday.Trim().ToLowerInvariant());

    public static string Normalize(string weekday) => weekday.Trim().ToLowerInvariant();

    /// <summary>ISO weekday name of a calendar date (used by the date lookup).</summary>
    public static string FromDate(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "monday",
        DayOfWeek.Tuesday => "tuesday",
        DayOfWeek.Wednesday => "wednesday",
        DayOfWeek.Thursday => "thursday",
        DayOfWeek.Friday => "friday",
        DayOfWeek.Saturday => "saturday",
        _ => "sunday"
    };

    /// <summary>Parses "HH:mm" (24h) into minutes from midnight; null when the value is absent or malformed.</summary>
    public static int? ParseTime(string? value)
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

    /// <summary>Normalizes "9:5" → "09:05"; returns null when unparseable.</summary>
    public static string? NormalizeTime(string? value)
        => ParseTime(value) is { } minutes ? $"{minutes / 60:D2}:{minutes % 60:D2}" : null;

    /// <summary>Half-open overlap test on minute windows: [aStart,aEnd) ∩ [bStart,bEnd) ≠ ∅.</summary>
    public static bool Overlaps(int aStart, int aEnd, int bStart, int bEnd)
        => aStart < bEnd && bStart < aEnd;
}
