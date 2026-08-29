using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Account;

public static class AccountMapper
{
    public static AccountListItemDto ToListItem(DomainAccount a)
        => new(a.Id, a.AccountName, a.AccountCode, a.AccountType, a.AccountCategory, a.Status, a.ParentAccountId, a.LogoDataUri, a.CountryRef);

    public static AccountExternalReferenceDto ToDto(AccountExternalReference r)
        => new(r.Id, r.SourceSystem, r.ExternalId, r.SourceEntity, r.DisplayName, r.Notes);

    public static AccountAttributeDto ToDto(AccountAttributeValue v)
        => new(v.AttributeCode, v.Value);

    public static AccountDetailDto ToDetail(
        DomainAccount a,
        IReadOnlyList<AccountExternalReferenceDto> externalReferences,
        IReadOnlyList<AccountAttributeDto> attributes)
        => new(
            a.Id, a.AccountName, a.AccountCode, a.AccountType, a.AccountCategory, a.ParentAccountId, a.Status,
            a.CountryRef, a.CityRef, a.DistrictRef, a.AddressLine, a.Latitude, a.Longitude,
            a.ResponsiblePersonName, a.ResponsiblePersonPhone, a.ResponsiblePersonEmail, a.Notes,
            a.CreatedAt, a.UpdatedAt, externalReferences, attributes, a.LogoDataUri);
}
