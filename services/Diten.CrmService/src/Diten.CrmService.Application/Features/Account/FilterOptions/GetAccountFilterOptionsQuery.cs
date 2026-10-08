using System.Text.RegularExpressions;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.FilterOptions;

/// <summary>
/// WP-VP-2B (mobile R3-a) — the filter options of the account list a FIELD user can read with only
/// <c>crm.account.read</c> (no <c>crm.territory.node.read</c>): the territory nodes that currently cover accounts and
/// the account-type codes present on those accounts. A READ query; no hierarchy write data (status, rules…) leaves it.
/// <para><paramref name="Scope"/>: <c>mine</c> (default) — the caller's current rep assignments (exact / subtree, the
/// WP-VP-2 rule via <see cref="RepTerritoryCoverage"/>); without one, <c>territoryStatus = unassigned</c> and the
/// tenant-wide options (K-5). <c>all</c> — the whole tenant (out-of-territory add, managers). <paramref name="Search"/>
/// narrows the option texts (territory code / name, type code), Turkish-insensitive.</para>
/// </summary>
public sealed record GetAccountFilterOptionsQuery(string? Scope = null, string? Search = null)
    : IRequest<Response<AccountFilterOptionsDto>>;

/// <param name="Scope"><c>mine</c> | <c>all</c> — the scope actually applied.</param>
/// <param name="TerritoryStatus">For <c>mine</c>: <c>assigned</c> | <c>unassigned</c>; null for <c>all</c>.</param>
public sealed record AccountFilterOptionsDto(
    string Scope,
    string? TerritoryStatus,
    IReadOnlyList<AccountFilterTerritoryDto> Territories,
    IReadOnlyList<string> AccountTypes);

public sealed record AccountFilterTerritoryDto(Guid NodeId, string? Code, string? Name, string? Level);

public static class AccountFilterScopes
{
    public const string Mine = "mine";
    public const string All = "all";
    public const string InvalidScopeCode = "invalid_filter_scope";
}

public sealed class GetAccountFilterOptionsQueryHandler
    : IRequestHandler<GetAccountFilterOptionsQuery, Response<AccountFilterOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly ITerritoryResourceAssignmentRepository _resourceAssignments;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryModelRepository _models;
    private readonly IAccountTerritoryAssignmentRepository _accountAssignments;
    private readonly IAccountRepository _accounts;

    public GetAccountFilterOptionsQueryHandler(
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

    public async Task<Response<AccountFilterOptionsDto>> Handle(
        GetAccountFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountFilterOptionsDto>.Fail("Tenant context is required.", 400);
        }

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? AccountFilterScopes.Mine : request.Scope.Trim().ToLowerInvariant();
        if (scope is not (AccountFilterScopes.Mine or AccountFilterScopes.All))
        {
            return Response<AccountFilterOptionsDto>.Fail(
                new[] { AccountFilterScopes.InvalidScopeCode, "scope must be 'mine' or 'all'." }, 400);
        }

        var now = DateTimeOffset.UtcNow;
        string? territoryStatus = null;
        List<AccountTerritoryAssignment> coverage;
        IReadOnlyCollection<Guid>? typeScope = null; // null ⇒ every tenant account

        if (scope == AccountFilterScopes.Mine)
        {
            var current = _caller.CallerResourceId is { } me
                ? await RepTerritoryCoverage.CurrentAssignmentsAsync(_resourceAssignments, _models, tenantId, me, now, cancellationToken)
                : Array.Empty<TerritoryResourceAssignment>();
            if (current.Count > 0)
            {
                territoryStatus = MyTerritoryStatuses.Assigned;
                var nodes = await RepTerritoryCoverage.CoveredNodesAsync(_nodes, tenantId, current, now, cancellationToken);
                coverage = await CurrentCoverageAsync(
                    await _accountAssignments.ListActiveByNodesAsync(tenantId, nodes, cancellationToken), tenantId, now, cancellationToken);
                typeScope = coverage.Select(a => a.AccountId).Distinct().ToList();
            }
            else
            {
                territoryStatus = MyTerritoryStatuses.Unassigned; // K-5: the tenant-wide options
                coverage = await TenantCoverageAsync(tenantId, now, cancellationToken);
            }
        }
        else
        {
            coverage = await TenantCoverageAsync(tenantId, now, cancellationToken);
        }

        var nodeIds = coverage.Select(a => a.TerritoryNodeId).Distinct().ToList();
        var nodeInfo = (await _nodes.ListByIdsAsync(tenantId, nodeIds, cancellationToken)).ToDictionary(n => n.Id);
        var territories = coverage
            .GroupBy(a => a.TerritoryNodeId)
            .Select(g =>
            {
                var node = nodeInfo.GetValueOrDefault(g.Key);
                return new AccountFilterTerritoryDto(
                    g.Key, node?.TerritoryCode ?? g.First().TerritoryNodeCode, node?.Name ?? g.First().TerritoryNodeName,
                    node?.TerritoryLevel);
            })
            .ToList();
        var types = await _accounts.ListDistinctAccountTypesAsync(tenantId, typeScope, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = new Regex(
                TurkishInsensitivePattern.Build(request.Search.Trim()), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            territories = territories.Where(t => pattern.IsMatch(t.Name ?? string.Empty) || pattern.IsMatch(t.Code ?? string.Empty)).ToList();
            types = types.Where(t => pattern.IsMatch(t)).ToList();
        }

        return Response<AccountFilterOptionsDto>.Success(new AccountFilterOptionsDto(
            scope,
            territoryStatus,
            territories.OrderBy(t => t.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true)).ToList(),
            types));
    }

    /// <summary>The tenant's CURRENT account coverage: active assignments of active models through the shared gate.</summary>
    private async Task<List<AccountTerritoryAssignment>> TenantCoverageAsync(
        Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var models = await _models.ListActiveAsync(tenantId, Guid.Empty, cancellationToken);
        if (models.Count == 0) return [];
        var assignments = await _accountAssignments.ListActiveByModelIdsAsync(
            tenantId, models.Select(m => m.Id).ToList(), cancellationToken);
        return await CurrentCoverageAsync(assignments, tenantId, now, cancellationToken);
    }

    private async Task<List<AccountTerritoryAssignment>> CurrentCoverageAsync(
        IReadOnlyList<AccountTerritoryAssignment> candidates, Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = candidates.Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, now)).ToList();
        if (open.Count == 0) return [];
        var modelMap = (await _models.ListByIdsAsync(tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(open), cancellationToken))
            .ToDictionary(m => m.Id);
        return TerritoryCoverageLifecyclePolicy.FilterCurrent(open, modelMap, now);
    }
}
