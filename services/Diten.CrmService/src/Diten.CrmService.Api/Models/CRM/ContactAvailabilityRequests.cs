namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0150 FU07 request bodies. Note what is NOT here: <c>TenantId</c> (server-resolved from the claim),
/// <c>ContactId</c> and <c>AccountId</c> (derived from the owning AccountContactLink — a payload can never claim a
/// contact/account pair the link does not have).
/// </summary>
public sealed record VisitPreferenceRequest(
    int? PreferredVisitDurationMinutes = null,
    string? PreferredVisitStartTime = null,
    string? PreferredVisitEndTime = null,
    string? AvoidVisitStartTime = null,
    string? AvoidVisitEndTime = null,
    bool AppointmentRequired = false,
    int? AppointmentLeadTimeDays = null,
    string? PreferredContactMethod = null,
    string? Notes = null);

/// <summary>Body for creating an availability row on a link (linkId comes from the route).</summary>
public sealed record CreateContactAvailabilityRequest(
    string Weekday,
    string StartTime,
    string EndTime,
    string AvailabilityType,
    string Source,
    string? Status = null,
    VisitPreferenceRequest? Preference = null,
    int? AverageVisitDurationMinutes = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null);

/// <summary>Body for updating an availability row. The owning link never changes — moving a schedule to another
/// location is a new row on that link.</summary>
public sealed record UpdateContactAvailabilityRequest(
    string Weekday,
    string StartTime,
    string EndTime,
    string AvailabilityType,
    string Source,
    string? Status = null,
    VisitPreferenceRequest? Preference = null,
    int? AverageVisitDurationMinutes = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null);

/// <summary>Body for creating a date-specific exception on a link (linkId comes from the route).</summary>
public sealed record CreateContactAvailabilityExceptionRequest(
    string Date,
    bool IsAvailable,
    string Source,
    string? StartTime = null,
    string? EndTime = null,
    string? Reason = null,
    string? Notes = null,
    string? Status = null);

public sealed record UpdateContactAvailabilityExceptionRequest(
    string Date,
    bool IsAvailable,
    string Source,
    string? StartTime = null,
    string? EndTime = null,
    string? Reason = null,
    string? Notes = null,
    string? Status = null);
