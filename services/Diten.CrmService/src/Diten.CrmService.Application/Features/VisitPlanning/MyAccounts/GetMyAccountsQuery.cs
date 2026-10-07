using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;

/// <summary>
/// WP-VP-2 (B-2, K-5 / K-6) — "my accounts": the accounts a rep's CURRENT territory assignments cover. A READ query
/// (no write, nothing audited). The rep is the caller; a <c>crm.visit-plan.read-all</c> holder may ask for another
/// <paramref name="ResourceId"/> (anyone else asking for one is 403 <c>resource_not_caller</c>).
/// </summary>
public sealed record GetMyAccountsQuery(
    string? Search = null,
    string? Type = null,
    int Page = 1,
    int PageSize = 25,
    string? ResourceId = null,
    // WP-VP-2B (R2) — "true" / "false" (case-insensitive) or absent; anything else is 400 invalid_has_active_contacts.
    string? HasActiveContacts = null) : IRequest<Response<MyAccountsDto>>;

/// <summary>
/// <see cref="TerritoryStatus"/> <c>assigned</c> carries the assignment nodes; <c>unassigned</c> (K-5) means the rep has no
/// current assignment, so every tenant account is returned and the screen warns.
/// </summary>
public sealed record MyAccountsDto(
    string TerritoryStatus,
    IReadOnlyList<MyTerritoryDto> Territories,
    IReadOnlyList<MyAccountItemDto> Items,
    long TotalCount,
    int Page,
    int PageSize);

public sealed record MyTerritoryDto(Guid TerritoryNodeId, string? Code, string? Name, string CoverageScope);

public sealed record MyAccountItemDto(
    Guid AccountId, string AccountName, string? AccountType, string? CityRef, string? DistrictRef, double? Latitude, double? Longitude,
    // WP-VP-2B (R1, additive) — the account's active contacts (the /accounts/{id}/contacts active rule).
    int ActiveContactCount = 0);

public static class MyTerritoryStatuses
{
    public const string Assigned = "assigned";
    public const string Unassigned = "unassigned";
}

/// <summary>The <c>territory-coverage-scope</c> values the rep assignment carries.</summary>
public static class TerritoryCoverageScopes
{
    public const string ExactTerritory = "exact-territory";
    public const string TerritorySubtree = "territory-subtree";
}

public sealed class GetMyAccountsQueryHandler : IRequestHandler<GetMyAccountsQuery, Response<MyAccountsDto>>
{
    private const int MaxPageSize = 100;

    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly ITerritoryResourceAssignmentRepository _resourceAssignments;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryModelRepository _models;
    private readonly IAccountTerritoryAssignmentRepository _accountAssignments;
    private readonly IAccountRepository _accounts;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactRepository _contacts;

    public GetMyAccountsQueryHandler(
        ITenantContext tenant,
        ICallerScope caller,
        ITerritoryResourceAssignmentRepository resourceAssignments,
        ITerritoryNodeRepository nodes,
        ITerritoryModelRepository models,
        IAccountTerritoryAssignmentRepository accountAssignments,
        IAccountRepository accounts,
        IAccountContactLinkRepository links,
        IContactRepository contacts)
    {
        _links = links;
        _contacts = contacts;
        _tenant = tenant;
        _caller = caller;
        _resourceAssignments = resourceAssignments;
        _nodes = nodes;
        _models = models;
        _accountAssignments = accountAssignments;
        _accounts = accounts;
    }

    public async Task<Response<MyAccountsDto>> Handle(GetMyAccountsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<MyAccountsDto>.Fail("Tenant context is required.", 400);
        }

        var (allowed, resourceId) = _caller.ResolveWriteResource(VisitPlanningPermissions.ReadAll, request.ResourceId);
        if (!allowed || resourceId is null)
        {
            return Response<MyAccountsDto>.Fail(
                new[] { VisitOwnership.ResourceNotCaller, "Only your own accounts can be listed." }, 403);
        }

        var (validFilter, hasActiveContacts) = AccountActiveContacts.ParseFilter(request.HasActiveContacts);
        if (!validFilter)
        {
            return Response<MyAccountsDto>.Fail(
                new[] { AccountActiveContacts.InvalidFilterCode, "hasActiveContacts must be 'true' or 'false'." }, 400);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var types = string.IsNullOrWhiteSpace(request.Type)
            ? null
            : request.Type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var now = DateTimeOffset.UtcNow;

        // WP-VP-2B (R2) — the tenant's accounts with an active contact, resolved once when the filter is asked for.
        var withActive = hasActiveContacts is null
            ? Array.Empty<Guid>()
            : await AccountActiveContacts.AccountIdsWithActiveContactsAsync(_links, _contacts, tenantId, cancellationToken);

        var current = await RepTerritoryCoverage.CurrentAssignmentsAsync(
            _resourceAssignments, _models, tenantId, resourceId, now, cancellationToken);
        if (current.Count == 0)
        {
            // K-5 — no current assignment: the whole tenant (the screen says so).
            var (allScope, allExcluded) = AccountActiveContacts.Apply(hasActiveContacts, null, withActive);
            var all = await _accounts.ListAsync(
                tenantId, search, page, pageSize, null, null, null, types, allScope, allExcluded, cancellationToken);
            return await OkAsync(MyTerritoryStatuses.Unassigned, Array.Empty<MyTerritoryDto>(), all.Items, all.Total, page, pageSize,
                tenantId, cancellationToken);
        }

        var nodeIds = current.Select(a => a.TerritoryId!.Value).Distinct().ToList();
        var nodeInfo = (await _nodes.ListByIdsAsync(tenantId, nodeIds, cancellationToken)).ToDictionary(n => n.Id);

        var coveredNodes = await RepTerritoryCoverage.CoveredNodesAsync(_nodes, tenantId, current, now, cancellationToken);

        var covered = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
            _accountAssignments, _models, tenantId, coveredNodes, now, cancellationToken);
        // WP-VP-2B (R2) — ANDed with the territory scope (true: covered ∩ active; false: covered \ active).
        var (scope, excluded) = AccountActiveContacts.Apply(hasActiveContacts, covered, withActive);
        var accounts = await _accounts.ListAsync(
            tenantId, search, page, pageSize, null, null, null, types, scope, excluded, cancellationToken);

        var territories = current
            .Select(a => new MyTerritoryDto(
                a.TerritoryId!.Value,
                nodeInfo.GetValueOrDefault(a.TerritoryId!.Value)?.TerritoryCode,
                nodeInfo.GetValueOrDefault(a.TerritoryId!.Value)?.Name,
                RepTerritoryCoverage.IsSubtree(a) ? TerritoryCoverageScopes.TerritorySubtree : TerritoryCoverageScopes.ExactTerritory))
            .GroupBy(t => t.TerritoryNodeId)
            .Select(g => g.First())
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)
            .ToList();

        return await OkAsync(MyTerritoryStatuses.Assigned, territories, accounts.Items, accounts.Total, page, pageSize,
            tenantId, cancellationToken);
    }

    private async Task<Response<MyAccountsDto>> OkAsync(
        string status, IReadOnlyList<MyTerritoryDto> territories, IReadOnlyList<Domain.Entities.Account> accounts, long total,
        int page, int pageSize, Guid tenantId, CancellationToken cancellationToken)
    {
        // WP-VP-2B (R1) — the page's active-contact counts in two bounded reads (never one per account).
        var counts = await AccountActiveContacts.CountForPageAsync(
            _links, _contacts, tenantId, accounts.Select(a => a.Id).ToList(), cancellationToken);
        return Response<MyAccountsDto>.Success(new MyAccountsDto(
            status,
            territories,
            accounts.Select(a => new MyAccountItemDto(
                a.Id, a.AccountName, a.AccountType, a.CityRef, a.DistrictRef, a.Latitude, a.Longitude,
                counts.GetValueOrDefault(a.Id))).ToList(),
            total,
            page,
            pageSize));
    }
}
