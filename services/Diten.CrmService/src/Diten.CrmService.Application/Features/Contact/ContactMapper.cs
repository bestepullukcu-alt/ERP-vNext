using DomainContact = Diten.CrmService.Domain.Entities.Contact;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Contact;

public static class ContactMapper
{
    /// <summary>Derives DisplayName from "FirstName LastName" when the supplied value is blank.</summary>
    public static string ResolveDisplayName(string? displayName, string? firstName, string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return $"{firstName?.Trim()} {lastName?.Trim()}".Trim();
    }

    public static ContactListItemDto ToListItem(DomainContact c)
        => new(c.Id, c.DisplayName, c.FirstName, c.LastName, c.ContactType, c.Status, c.ProfessionalTitle, c.Email, c.Phone, c.PhotoDataUri);

    public static ContactSearchResultDto ToSearchResult(DomainContact c)
        => new(c.Id, c.DisplayName, c.ContactType, c.Status);

    public static ContactExternalReferenceDto ToDto(ContactExternalReference r)
        => new(r.Id, r.SourceSystem, r.ExternalId, r.SourceEntity, r.DisplayName, r.Notes);

    public static ContactDetailDto ToDetail(DomainContact c, IReadOnlyList<ContactExternalReferenceDto> externalReferences)
        => new(
            c.Id, c.DisplayName, c.FirstName, c.LastName, c.ContactType, c.Status,
            c.ProfessionalTitle, c.Specialty, c.Department, c.Phone, c.Email, c.Notes,
            c.CreatedAt, c.UpdatedAt, externalReferences,
            c.CountryRef, c.CityRef, c.DistrictRef, c.AddressLine, c.PostalCode, c.PreferredLanguage, c.PhoneCountryCode, c.Gender, c.PhotoDataUri);
}
