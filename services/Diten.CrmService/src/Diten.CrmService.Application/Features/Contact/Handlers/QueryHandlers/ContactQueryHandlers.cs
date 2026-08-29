using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account; // PagedResult<T>
using Diten.CrmService.Application.Features.ConsentPreference;
using Diten.CrmService.Application.Features.Contact.Queries;
using Diten.CrmService.Application.Features.Territory.AccountAssignments; // AccountCurrentCoverageResolver
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Application.Features.Contact.Handlers.QueryHandlers;

public sealed class GetContactByIdHandler : IRequestHandler<GetContactByIdQuery, Response<ContactDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;

    public GetContactByIdHandler(ITenantContext tenant, IContactRepository contacts, IContactExternalReferenceRepository externalRefs)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
    }

    public async Task<Response<ContactDetailDto>> Handle(GetContactByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContactDetailDto>.Fail("Tenant context is required.", 400);
        }

        var contact = await _contacts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (contact is null)
        {
            return Response<ContactDetailDto>.Fail("Contact not found.", 404);
        }

        var externalRefs = (await _externalRefs.ListByContactAsync(tenantId, contact.Id, cancellationToken))
            .Select(ContactMapper.ToDto).ToList();

        return Response<ContactDetailDto>.Success(ContactMapper.ToDetail(contact, externalRefs));
    }
}

public sealed class GetContactOverviewHandler : IRequestHandler<GetContactOverviewQuery, Response<ContactOverviewDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IAccountContactLinkRepository _links;
    private readonly IAccountRepository _accounts;
    private readonly IContactConsentPreferenceReader _consentPreference;
    private readonly IAccountTerritoryAssignmentRepository _territoryAssignments;
    private readonly ITerritoryModelRepository _territoryModels;
    private readonly ILogger<GetContactOverviewHandler> _logger;

    public GetContactOverviewHandler(
        ITenantContext tenant, IContactRepository contacts, IContactExternalReferenceRepository externalRefs,
        IAccountContactLinkRepository links, IAccountRepository accounts,
        IContactConsentPreferenceReader consentPreference,
        IAccountTerritoryAssignmentRepository territoryAssignments, ITerritoryModelRepository territoryModels,
        ILogger<GetContactOverviewHandler> logger)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _links = links;
        _accounts = accounts;
        _consentPreference = consentPreference;
        _territoryAssignments = territoryAssignments;
        _territoryModels = territoryModels;
        _logger = logger;
    }

    public async Task<Response<ContactOverviewDto>> Handle(GetContactOverviewQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContactOverviewDto>.Fail("Tenant context is required.", 400);
        }

        var contact = await _contacts.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (contact is null)
        {
            return Response<ContactOverviewDto>.Fail("Contact not found.", 404);
        }

        var externalRefs = (await _externalRefs.ListByContactAsync(tenantId, contact.Id, cancellationToken))
            .Select(ContactMapper.ToDto).ToList();
        var detail = ContactMapper.ToDetail(contact, externalRefs);

        // FU03 — real linked accounts (AccountContactLink join). Best-effort: skip links whose account was soft-deleted.
        var links = await _links.ListByContactAsync(tenantId, contact.Id, cancellationToken);
        var linkedAccounts = new List<ContactAccountLinkSummaryDto>();
        foreach (var link in links)
        {
            var account = await _accounts.GetByIdAsync(tenantId, link.AccountId, cancellationToken);
            if (account is not null)
            {
                linkedAccounts.Add(new ContactAccountLinkSummaryDto(
                    link.Id, link.AccountId, account.AccountName, account.AccountCode, account.AccountType,
                    link.RoleCode, link.IsPrimary, link.Status));
            }
        }

        // FU05 — read-only consent/preference seam (MOD-0164). Masked when the caller carries neither
        // crm.contact.consent.read nor crm.contact.preference.read; otherwise fail-soft (reader never breaks 360).
        ContactConsentPreferenceSummaryDto consentPreference;
        if (!request.CanReadConsent && !request.CanReadPreference)
        {
            consentPreference = ContactConsentPreferenceSummaryDto.NotAuthorized(contact.Id);
        }
        else
        {
            try
            {
                consentPreference = await _consentPreference.GetSummaryAsync(tenantId, contact.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                // Soft dependency: an erroring MOD-0164 seam must never break Contact 360.
                _logger.LogWarning(ex, "Consent/preference seam failed for contact {ContactId}; returning not-available.", contact.Id);
                consentPreference = ContactConsentPreferenceSummaryDto.NotAvailable(contact.Id);
            }
        }

        // MOD-0151 — a contact has no territory of its own; its coverage is the current territory of each linked
        // account (can be several). Read-only projection; degrades to empty when no linked account is currently covered.
        var accountById = linkedAccounts
            .GroupBy(a => a.AccountId)
            .ToDictionary(g => g.Key, g => g.First());
        var coverage = await AccountCurrentCoverageResolver.ResolveAsync(
            _territoryAssignments, _territoryModels, tenantId, accountById.Keys.ToList(), DateTimeOffset.UtcNow, cancellationToken);
        var territoryCoverage = coverage
            .Select(c => new ContactTerritoryCoverageDto(
                c.AccountId,
                accountById.GetValueOrDefault(c.AccountId)?.AccountName ?? string.Empty,
                accountById.GetValueOrDefault(c.AccountId)?.AccountCode ?? string.Empty,
                c.TerritoryNodeId, c.TerritoryNodeCode, c.TerritoryNodeName, c.CountryScope,
                c.AssignmentStatus, c.EffectiveFrom, c.EffectiveTo))
            .OrderBy(x => x.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.TerritoryNodeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var overview = new ContactOverviewDto(detail, linkedAccounts, consentPreference, territoryCoverage);
        return Response<ContactOverviewDto>.Success(overview);
    }
}

public sealed class ListContactsHandler : IRequestHandler<ListContactsQuery, Response<PagedResult<ContactListItemDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IAccountContactLinkRepository _links;
    private readonly IAccountTerritoryAssignmentRepository _territoryAssignments;
    private readonly ITerritoryModelRepository _territoryModels;

    public ListContactsHandler(
        ITenantContext tenant,
        IContactRepository contacts,
        IAccountContactLinkRepository links,
        IAccountTerritoryAssignmentRepository territoryAssignments,
        ITerritoryModelRepository territoryModels)
    {
        _tenant = tenant;
        _contacts = contacts;
        _links = links;
        _territoryAssignments = territoryAssignments;
        _territoryModels = territoryModels;
    }

    public async Task<Response<PagedResult<ContactListItemDto>>> Handle(ListContactsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PagedResult<ContactListItemDto>>.Fail("Tenant context is required.", 400);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        var (items, total) = await _contacts.ListAsync(tenantId, request.Search, page, pageSize, cancellationToken);
        var dtos = items.Select(ContactMapper.ToListItem).ToList();

        await EnrichLinkedTerritoryAsync(tenantId, dtos, cancellationToken);

        return Response<PagedResult<ContactListItemDto>>.Success(new PagedResult<ContactListItemDto>(dtos, total, page, pageSize));
    }

    /// <summary>Projects the contact's current territory coverage onto each list row so the grid can show/filter it.
    /// A contact's coverage is the union of its linked accounts' current territory nodes (and their models' country
    /// scopes) — a contact linked to several accounts can therefore carry several nodes / scopes.</summary>
    private async Task EnrichLinkedTerritoryAsync(Guid tenantId, List<ContactListItemDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0) return;

        var contactIds = dtos.Select(d => d.Id).ToList();
        var links = await _links.ListByContactIdsAsync(tenantId, contactIds, cancellationToken);
        if (links.Count == 0) return;

        var accountsByContact = links
            .GroupBy(l => l.ContactId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.AccountId).Distinct().ToList());

        var coverage = await AccountCurrentCoverageResolver.ResolveAsync(
            _territoryAssignments, _territoryModels, tenantId,
            links.Select(l => l.AccountId).Distinct().ToList(), DateTimeOffset.UtcNow, cancellationToken);
        if (coverage.Count == 0) return;

        var coverageByAccount = coverage
            .GroupBy(c => c.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var i = 0; i < dtos.Count; i++)
        {
            if (!accountsByContact.TryGetValue(dtos[i].Id, out var accountIds)) continue;

            var rows = accountIds
                .Where(coverageByAccount.ContainsKey)
                .SelectMany(id => coverageByAccount[id])
                .ToList();
            if (rows.Count == 0) continue;

            var scopes = rows
                .Select(r => r.CountryScope)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var nodeNames = rows
                .Select(r => r.TerritoryNodeName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            dtos[i] = dtos[i] with { TerritoryCountryScopes = scopes, TerritoryNodeNames = nodeNames };
        }
    }
}

public sealed class SearchContactsHandler : IRequestHandler<SearchContactsQuery, Response<IReadOnlyList<ContactSearchResultDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;

    public SearchContactsHandler(ITenantContext tenant, IContactRepository contacts)
    {
        _tenant = tenant;
        _contacts = contacts;
    }

    public async Task<Response<IReadOnlyList<ContactSearchResultDto>>> Handle(SearchContactsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ContactSearchResultDto>>.Fail("Tenant context is required.", 400);
        }

        var limit = request.Limit is < 1 or > 50 ? 20 : request.Limit;
        var (items, _) = await _contacts.ListAsync(tenantId, request.Search, 1, limit, cancellationToken);
        IReadOnlyList<ContactSearchResultDto> results = items.Select(ContactMapper.ToSearchResult).ToList();

        return Response<IReadOnlyList<ContactSearchResultDto>>.Success(results);
    }
}
