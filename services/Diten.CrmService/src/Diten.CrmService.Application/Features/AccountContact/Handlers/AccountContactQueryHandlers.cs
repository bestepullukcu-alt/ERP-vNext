using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.AccountContact.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountContact.Handlers;

public sealed class ListContactsForAccountHandler
    : IRequestHandler<ListContactsForAccountQuery, Response<IReadOnlyList<AccountRelatedContactDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;

    public ListContactsForAccountHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts, IAccountContactLinkRepository links)
    {
        _tenant = tenant;
        _accounts = accounts;
        _contacts = contacts;
        _links = links;
    }

    public async Task<Response<IReadOnlyList<AccountRelatedContactDto>>> Handle(ListContactsForAccountQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<AccountRelatedContactDto>>.Fail("Tenant context is required.", 400);
        }

        if (await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken) is null)
        {
            return Response<IReadOnlyList<AccountRelatedContactDto>>.Fail("Account not found.", 404);
        }

        var links = await _links.ListByAccountAsync(tenantId, request.AccountId, cancellationToken);

        // Resolve contacts once, then map ReportsToContactId → manager display name for the org-hierarchy view.
        var contactsById = new Dictionary<Guid, Diten.CrmService.Domain.Entities.Contact>();
        foreach (var link in links)
        {
            if (!contactsById.ContainsKey(link.ContactId)
                && await _contacts.GetByIdAsync(tenantId, link.ContactId, cancellationToken) is { } c)
            {
                contactsById[link.ContactId] = c;
            }
        }

        var rows = new List<AccountRelatedContactDto>();
        foreach (var link in links)
        {
            // Skip links whose contact has since been soft-deleted (join is best-effort; never fabricate).
            if (!contactsById.TryGetValue(link.ContactId, out var contact))
            {
                continue;
            }
            string? reportsToName = link.ReportsToContactId is { } pid && contactsById.TryGetValue(pid, out var parent)
                ? parent.DisplayName
                : null;
            rows.Add(AccountContactMapper.ToRelatedContact(link, contact, reportsToName));
        }

        return Response<IReadOnlyList<AccountRelatedContactDto>>.Success(rows);
    }
}

public sealed class ListAccountsForContactHandler
    : IRequestHandler<ListAccountsForContactQuery, Response<IReadOnlyList<ContactLinkedAccountDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;

    public ListAccountsForContactHandler(ITenantContext tenant, IAccountRepository accounts, IContactRepository contacts, IAccountContactLinkRepository links)
    {
        _tenant = tenant;
        _accounts = accounts;
        _contacts = contacts;
        _links = links;
    }

    public async Task<Response<IReadOnlyList<ContactLinkedAccountDto>>> Handle(ListAccountsForContactQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ContactLinkedAccountDto>>.Fail("Tenant context is required.", 400);
        }

        if (await _contacts.GetByIdAsync(tenantId, request.ContactId, cancellationToken) is null)
        {
            return Response<IReadOnlyList<ContactLinkedAccountDto>>.Fail("Contact not found.", 404);
        }

        var links = await _links.ListByContactAsync(tenantId, request.ContactId, cancellationToken);
        var rows = new List<ContactLinkedAccountDto>();
        foreach (var link in links)
        {
            var account = await _accounts.GetByIdAsync(tenantId, link.AccountId, cancellationToken);
            if (account is not null)
            {
                rows.Add(AccountContactMapper.ToLinkedAccount(link, account));
            }
        }

        return Response<IReadOnlyList<ContactLinkedAccountDto>>.Success(rows);
    }
}

public sealed class GetAccountContactLinkByIdHandler : IRequestHandler<GetAccountContactLinkByIdQuery, Response<AccountContactLinkDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountContactLinkRepository _links;

    public GetAccountContactLinkByIdHandler(ITenantContext tenant, IAccountContactLinkRepository links)
    {
        _tenant = tenant;
        _links = links;
    }

    public async Task<Response<AccountContactLinkDto>> Handle(GetAccountContactLinkByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountContactLinkDto>.Fail("Tenant context is required.", 400);
        }

        var link = await _links.GetByIdAsync(tenantId, request.LinkId, cancellationToken);
        if (link is null || link.AccountId != request.AccountId)
        {
            return Response<AccountContactLinkDto>.Fail("Account-contact link not found.", 404);
        }

        return Response<AccountContactLinkDto>.Success(AccountContactMapper.ToDto(link));
    }
}
