namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0150 FU07 — a date-specific override of the weekly <see cref="ContactAvailability"/> pattern for one
/// AccountContactLink (leave, congress, surgery day, temporary relocation). The exception is <b>stronger</b> than the
/// weekly pattern for its date (pack D12).
/// <para>
/// <c>IsAvailable=false</c> = not visitable that day. <c>IsAvailable=true</c> + Start/End = an ad-hoc window that need
/// not exist in the weekly pattern. One active exception per (link, date); closing uses inactive/archived — there is
/// no hard delete.
/// </para>
/// </summary>
public sealed class ContactAvailabilityException : EntityBase
{
    public Guid AccountContactLinkId { get; set; }

    /// <summary>Navigation copy of the link's ContactId (derived, never client-supplied).</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation copy of the link's AccountId (derived, never client-supplied).</summary>
    public Guid AccountId { get; set; }

    /// <summary>The calendar date this exception applies to, stored as "yyyy-MM-dd" (a local calendar day, not an
    /// instant — same wall-clock reasoning as the availability windows).</summary>
    public string Date { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }

    /// <summary>Ad-hoc window start ("HH:mm"), only meaningful when <see cref="IsAvailable"/> is true.</summary>
    public string? StartTime { get; set; }

    /// <summary>Ad-hoc window end ("HH:mm"), only meaningful when <see cref="IsAvailable"/> is true.</summary>
    public string? EndTime { get; set; }

    /// <summary>Free-text business reason (congress, leave, surgery…). Optional; the MOD-0048
    /// <c>availability-exception-reason</c> set is a proposal and is NOT required here (no hardcoded fallback either).</summary>
    public string? Reason { get; set; }

    public string? Notes { get; set; }

    /// <summary>MOD-0048 <c>contact-availability-source</c> published value.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>MOD-0048 <c>contact-availability-status</c> published value (active / inactive / archived).</summary>
    public string Status { get; set; } = AvailabilityLifecycle.Active;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Parses <see cref="Date"/>; null when malformed (defensive — writes always normalize it).</summary>
    public DateOnly? ParsedDate()
        => DateOnly.TryParse(Date, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
