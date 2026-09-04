using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account; // PagedResult<T> (shared generic paging record)
using MediatR;

namespace Diten.CrmService.Application.Features.Contact.Queries;

public sealed record GetContactByIdQuery(Guid Id) : IRequest<Response<ContactDetailDto>>;

/// <summary>Contact 360. <paramref name="CanReadConsent"/>/<paramref name="CanReadPreference"/> are resolved from the
/// caller's claims by the controller — the consent/preference seam block is masked when neither is present.</summary>
public sealed record GetContactOverviewQuery(Guid Id, bool CanReadConsent = false, bool CanReadPreference = false)
    : IRequest<Response<ContactOverviewDto>>;

public sealed record ListContactsQuery(
    string? Search, int Page, int PageSize, string? SortBy = null, string? SortDir = null,
    string? Status = null, string? ContactType = null)
    : IRequest<Response<PagedResult<ContactListItemDto>>>;

/// <summary>Minimal typeahead used later by the Account-link tasks (FU03). Returns lightweight results only.</summary>
public sealed record SearchContactsQuery(string? Search, int Limit)
    : IRequest<Response<IReadOnlyList<ContactSearchResultDto>>>;
