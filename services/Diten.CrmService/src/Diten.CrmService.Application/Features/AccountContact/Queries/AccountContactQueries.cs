using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountContact.Queries;

/// <summary>Account 360 "Related Contacts" (also serves GET /accounts/{id}/contacts and /accounts/{id}/related-contacts).</summary>
public sealed record ListContactsForAccountQuery(Guid AccountId)
    : IRequest<Response<IReadOnlyList<AccountRelatedContactDto>>>;

/// <summary>Contact 360 "Linked Accounts" (also serves GET /contacts/{id}/accounts and /contacts/{id}/linked-accounts).</summary>
public sealed record ListAccountsForContactQuery(Guid ContactId)
    : IRequest<Response<IReadOnlyList<ContactLinkedAccountDto>>>;

public sealed record GetAccountContactLinkByIdQuery(Guid AccountId, Guid LinkId)
    : IRequest<Response<AccountContactLinkDto>>;
