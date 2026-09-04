using AccountContactLink = Diten.CrmService.Domain.Entities.AccountContactLink;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;

namespace Diten.CrmService.Application.Features.AccountContact;

public static class AccountContactMapper
{
    public static AccountContactLinkDto ToDto(AccountContactLink l)
        => new(l.Id, l.AccountId, l.ContactId, l.RoleCode, l.IsPrimary, l.Status, l.ValidFrom, l.ValidTo, l.Notes, l.CreatedAt, l.UpdatedAt, l.ReportsToContactId);

    public static AccountRelatedContactDto ToRelatedContact(AccountContactLink l, DomainContact c, string? reportsToName = null)
        => new(l.Id, l.AccountId, l.ContactId, c.DisplayName, c.ContactType, l.RoleCode, l.IsPrimary, l.Status, c.Phone, c.Email, l.ReportsToContactId, reportsToName, c.Specialty, c.PhotoDataUri);

    public static ContactLinkedAccountDto ToLinkedAccount(AccountContactLink l, DomainAccount a)
        => new(l.Id, l.ContactId, l.AccountId, a.AccountName, a.AccountCode, a.AccountType, l.RoleCode, l.IsPrimary, l.Status);
}
