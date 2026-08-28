namespace Diten.CrmService.Application.Features.ContactAvailability;

/// <summary>MOD-0150 FU07 — MOD-0048 set codes this feature consumes. CRM never seeds or hardcodes these values;
/// an unpublished required set is a controlled 400 (fail-closed), never a local fallback list.</summary>
public static class ContactAvailabilityReferenceSets
{
    public const string Type = "contact-availability-type";
    public const string Source = "contact-availability-source";
    public const string Status = "contact-availability-status";

    public static readonly IReadOnlyList<string> All = new[] { Type, Source, Status };
}

/// <summary>
/// MOD-0150 FU07 permission keys. Canonical targets are defined here but the endpoints deliberately run on the
/// documented FALLBACK (<c>crm.contact.read</c> / <c>crm.contact.update</c>) until the RBAC catalog carries them —
/// same pattern as MOD-0151 FU08. This file seeds NOTHING. There is no delete key because there is no hard delete.
/// </summary>
public static class ContactAvailabilityPermissions
{
    public const string Read = "crm.contact.availability.read";
    public const string Manage = "crm.contact.availability.manage";

    /// <summary>Temporary fallback used by the endpoints (catalog alignment follow-up: MOD-0150-FU-RBAC).</summary>
    public const string ReadFallback = "crm.contact.read";

    public const string ManageFallback = "crm.contact.update";
}

/// <summary>Visit preference projection (link-scoped; contact-level defaults are a fallback only).</summary>
public sealed record VisitPreferenceDto(
    int? PreferredVisitDurationMinutes,
    string? PreferredVisitStartTime,
    string? PreferredVisitEndTime,
    string? AvoidVisitStartTime,
    string? AvoidVisitEndTime,
    bool AppointmentRequired,
    int? AppointmentLeadTimeDays,
    string? PreferredContactMethod,
    string? Notes);

/// <summary>One availability row of one AccountContactLink (weekly pattern).</summary>
public sealed record ContactAvailabilityDto(
    Guid Id,
    Guid AccountContactLinkId,
    Guid ContactId,
    string? ContactDisplayName,
    Guid AccountId,
    string? AccountDisplayName,
    string? AccountCode,
    string Weekday,
    string StartTime,
    string EndTime,
    VisitPreferenceDto Preference,
    // Surfaced at row level for grids/consumers; AppointmentRequired/LeadTime are read from Preference (single source).
    bool AppointmentRequired,
    int? AppointmentLeadTimeDays,
    int? AverageVisitDurationMinutes,
    string AvailabilityType,
    string Source,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

/// <summary>One date-specific exception row (stronger than the weekly pattern for its date).</summary>
public sealed record ContactAvailabilityExceptionDto(
    Guid Id,
    Guid AccountContactLinkId,
    Guid ContactId,
    string? ContactDisplayName,
    Guid AccountId,
    string? AccountDisplayName,
    string Date,
    bool IsAvailable,
    string? StartTime,
    string? EndTime,
    string? Reason,
    string? Notes,
    string Source,
    string Status,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

/// <summary>Availability + exceptions of a single AccountContactLink (the link panel payload).</summary>
public sealed record LinkAvailabilityDto(
    Guid AccountContactLinkId,
    Guid ContactId,
    string? ContactDisplayName,
    Guid AccountId,
    string? AccountDisplayName,
    string? AccountCode,
    string? RoleCode,
    bool IsPrimary,
    bool LinkIsActive,
    IReadOnlyList<ContactAvailabilityDto> Availability,
    IReadOnlyList<ContactAvailabilityExceptionDto> Exceptions);

/// <summary>
/// MOD-0150 FU07 lookup result — the MOD-0151 FU09A / MOD-0155 consumption seam. It returns <b>rows, never a
/// verdict</b>: no ordering, no distance, no score, no plan. Missing data is <c>unknown</c>, never "not available"
/// (pack D15).
/// </summary>
public sealed record ContactAvailabilityLookupRowDto(
    Guid AccountContactLinkId,
    Guid ContactId,
    string? ContactDisplayName,
    Guid AccountId,
    string? AccountDisplayName,
    string Weekday,
    string? AvailableWindow,
    string? PreferredWindow,
    string? AvoidWindow,
    bool AppointmentRequired,
    int? AppointmentLeadTimeDays,
    int? AverageVisitDurationMinutes,
    string AvailabilityStatus,
    bool ExceptionApplied,
    string? ExceptionReason,
    IReadOnlyList<string> ReasonCodes);

/// <summary>Lookup envelope: the queried date + its ISO weekday + the rows.</summary>
public sealed record ContactAvailabilityLookupDto(
    string Date,
    string Weekday,
    IReadOnlyList<ContactAvailabilityLookupRowDto> Rows);

/// <summary>
/// Stable, machine-readable lookup statuses/reasons. Deliberately mirrors the MOD-0151 FU09A vocabulary so the
/// readiness consumer does not have to translate. Localized text is a UI concern; these codes never change.
/// </summary>
public static class AvailabilityLookupStatus
{
    /// <summary>Availability data exists and the date is inside a window.</summary>
    public const string Available = "available";

    /// <summary>Explicit data says the date is not visitable, such as an unavailable exception.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>No matching availability data — NOT the same as unavailable (pack D15 / MOD-0151 R11).</summary>
    public const string Unknown = "unknown";
}

/// <summary>Reason codes attached to lookup rows (a row may carry several).</summary>
public static class AvailabilityReasonCodes
{
    public const string AvailabilityOk = "availability_ok";
    public const string NoAvailabilityData = "no_availability_data";
    public const string NotAvailableOnDay = "not_available_on_day";
    public const string ExceptionUnavailable = "exception_unavailable";
    public const string ExceptionWindowApplied = "exception_window_applied";
    public const string OutsideEffectiveWindow = "outside_effective_window";
    public const string AppointmentRequired = "appointment_required";
    public const string AvoidWindowDefined = "avoid_window_defined";
    public const string PreferredWindowDefined = "preferred_window_defined";
    public const string LinkInactive = "link_inactive";
    public const string AvailabilityInactive = "availability_inactive";
}
