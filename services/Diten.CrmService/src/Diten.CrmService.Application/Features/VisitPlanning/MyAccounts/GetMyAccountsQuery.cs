using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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
    string? ResourceId = null) : IRequest<Response<MyAccountsDto>>;

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
    Guid AccountId, string AccountName, string? AccountType, string? CityRef, string? DistrictRef, double? Latitude, double? Longitude);

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

    public GetMyAccountsQueryHandler(
        ITenantContext tenant,
        ICallerScope caller,
        ITerritoryResourceAssignmentRepository resourceAssignments,
        ITerritoryNodeRepository nodes,
        ITerritoryModelRepository models,
        IAccountTerritoryAssignmentRepository accountAssignments,
        IAccountRepository accounts)
    {
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

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var types = string.IsNullOrWhiteSpace(request.Type)
            ? null
            : request.Type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var now = DateTimeOffset.UtcNow;

        var current = await CurrentAssignmentsAsync(tenantId, resourceId, now, cancellationToken);
        if (current.Count == 0)
        {
            // K-5 — no current assignment: the whole tenant (the screen says so).
            var all = await _accounts.ListAsync(
                tenantId, search, page, pageSize, null, null, null, types, null, cancellationToken);
            return Ok(MyTerritoryStatuses.Unassigned, Array.Empty<MyTerritoryDto>(), all.Items, all.Total, page, pageSize);
        }

        var nodeIds = current.Select(a => a.TerritoryId!.Value).Distinct().ToList();
        var nodeInfo = (await _nodes.ListByIdsAsync(tenantId, nodeIds, cancellationToken)).ToDictionary(n => n.Id);

        var exact = current.Where(a => !IsSubtree(a)).Select(a => a.TerritoryId!.Value).ToList();
        var subtree = current.Where(IsSubtree).Select(a => a.TerritoryId!.Value).ToList();
        var coveredNodes = new HashSet<Guid>(exact);
        if (subtree.Count > 0)
        {
            coveredNodes.UnionWith(await TerritorySubtree.ExpandAsync(_nodes, tenantId, subtree, now, cancellationToken));
        }

        var covered = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
            _accountAssignments, _models, tenantId, coveredNodes, now, cancellationToken);
        var accounts = await _accounts.ListAsync(
            tenantId, search, page, pageSize, null, null, null, types, covered, cancellationToken);

        var territories = current
            .Select(a => new MyTerritoryDto(
                a.TerritoryId!.Value,
                nodeInfo.GetValueOrDefault(a.TerritoryId!.Value)?.TerritoryCode,
                nodeInfo.GetValueOrDefault(a.TerritoryId!.Value)?.Name,
                IsSubtree(a) ? TerritoryCoverageScopes.TerritorySubtree : TerritoryCoverageScopes.ExactTerritory))
            .GroupBy(t => t.TerritoryNodeId)
            .Select(g => g.First())
            .OrderBy(t => t.Name, StringComparer.CurrentCulture)
            .ToList();

        return Ok(MyTerritoryStatuses.Assigned, territories, accounts.Items, accounts.Total, page, pageSize);
    }

    /// <summary>The rep's assignments that hold NOW: active, not deleted, a node set, the validity window covering now,
    /// and the owning model operationally current (the shared coverage lifecycle model gate).</summary>
    private async Task<IReadOnlyList<TerritoryResourceAssignment>> CurrentAssignmentsAsync(
        Guid tenantId, string resourceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var mine = (await _resourceAssignments.ListByResourceAsync(tenantId, resourceId, cancellationToken))
            .Where(a => !a.IsDeleted
                        && a.TerritoryId is { } t && t != Guid.Empty
                        && string.Equals(a.Status, "active", StringComparison.OrdinalIgnoreCase)
                        && a.ValidFrom <= now
                        && (a.ValidTo is null || a.ValidTo >= now))
            .ToList();
        if (mine.Count == 0) return mine;

        var models = (await _models.ListByIdsAsync(tenantId, mine.Select(a => a.ModelId).Distinct().ToList(), cancellationToken))
            .ToDictionary(m => m.Id);
        return mine.Where(a => TerritoryCoverageLifecyclePolicy.IsModelCurrent(models.GetValueOrDefault(a.ModelId), now)).ToList();
    }

    private static bool IsSubtree(TerritoryResourceAssignment a)
        => string.Equals(a.CoverageScope, TerritoryCoverageScopes.TerritorySubtree, StringComparison.OrdinalIgnoreCase);

    private static Response<MyAccountsDto> Ok(
        string status, IReadOnlyList<MyTerritoryDto> territories, IReadOnlyList<Domain.Entities.Account> accounts, long total, int page, int pageSize)
        => Response<MyAccountsDto>.Success(new MyAccountsDto(
            status,
            territories,
            accounts.Select(a => new MyAccountItemDto(
                a.Id, a.AccountName, a.AccountType, a.CityRef, a.DistrictRef, a.Latitude, a.Longitude)).ToList(),
            total,
            page,
            pageSize));
}
