using Diten.CrmService.Domain.Entities;
using DomainAvailability = Diten.CrmService.Domain.Entities.ContactAvailability;
using DomainException = Diten.CrmService.Domain.Entities.ContactAvailabilityException;

namespace Diten.CrmService.Application.Features.ContactAvailability;

/// <summary>MOD-0150 FU07 entity → DTO projections. Contact/account display names are enrichment only; the DTO never
/// carries Contact master fields beyond the identity + display name.</summary>
internal static class ContactAvailabilityMapper
{
    public static VisitPreferenceDto ToDto(VisitPreference preference) => new(
        preference.PreferredVisitDurationMinutes,
        preference.PreferredVisitStartTime,
        preference.PreferredVisitEndTime,
        preference.AvoidVisitStartTime,
        preference.AvoidVisitEndTime,
        preference.AppointmentRequired,
        preference.AppointmentLeadTimeDays,
        preference.PreferredContactMethod,
        preference.Notes);

    public static ContactAvailabilityDto ToDto(
        DomainAvailability availability,
        string? contactDisplayName = null,
        string? accountDisplayName = null,
        string? accountCode = null)
    {
        var preference = availability.Preference ?? new VisitPreference();
        return new ContactAvailabilityDto(
            availability.Id,
            availability.AccountContactLinkId,
            availability.ContactId,
            contactDisplayName,
            availability.AccountId,
            accountDisplayName,
            accountCode,
            availability.Weekday,
            availability.StartTime,
            availability.EndTime,
            ToDto(preference),
            preference.AppointmentRequired,
            preference.AppointmentLeadTimeDays,
            availability.AverageVisitDurationMinutes,
            availability.AvailabilityType,
            availability.Source,
            availability.Status,
            availability.EffectiveFrom,
            availability.EffectiveTo,
            availability.Notes,
            availability.CreatedAt,
            availability.CreatedBy,
            availability.UpdatedAt,
            availability.UpdatedBy);
    }

    public static ContactAvailabilityExceptionDto ToDto(
        DomainException exception,
        string? contactDisplayName = null,
        string? accountDisplayName = null) => new(
        exception.Id,
        exception.AccountContactLinkId,
        exception.ContactId,
        contactDisplayName,
        exception.AccountId,
        accountDisplayName,
        exception.Date,
        exception.IsAvailable,
        exception.StartTime,
        exception.EndTime,
        exception.Reason,
        exception.Notes,
        exception.Source,
        exception.Status,
        exception.CreatedAt,
        exception.CreatedBy,
        exception.UpdatedAt,
        exception.UpdatedBy);

    /// <summary>"09:00–13:00" window label, or null when either bound is missing.</summary>
    public static string? Window(string? start, string? end)
        => string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end) ? null : $"{start}-{end}";
}
