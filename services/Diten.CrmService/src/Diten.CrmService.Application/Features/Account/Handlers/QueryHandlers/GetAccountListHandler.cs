using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;

public sealed class GetAccountListHandler : IRequestHandler<GetAccountListQuery, Response<PagedResult<AccountListItemDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly IAccountTerritoryAssignmentRepository _territoryAssignments;
    private readonly ITerritoryModelRepository _territoryModels;

    public GetAccountListHandler(
        ITenantContext tenant,
        IAccountRepository accounts,
        IAccountTerritoryAssignmentRepository territoryAssignments,
        ITerritoryModelRepository territoryModels)
    {
        _tenant = tenant;
        _accounts = accounts;
        _territoryAssignments = territoryAssignments;
        _territoryModels = territoryModels;
    }

    public async Task<Response<PagedResult<AccountListItemDto>>> Handle(GetAccountListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PagedResult<AccountListItemDto>>.Fail("Tenant context is required.", 400);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        // Multi-select chips arrive as comma-separated codes; split into distinct, non-empty equality values.
        var statuses = SplitFilterCsv(request.Status);
        var accountTypes = SplitFilterCsv(request.AccountType);

        // MOD-0151 territory-coverage chips (Territory Node ids / Country Scope codes). Resolve them to the set of
        // current-coverage account ids and AND that onto the account query. Null when neither chip is set (predicate
        // skipped); an empty set when a chip was set but nothing is currently covered (yields zero rows).
        var accountIdScope = await ResolveTerritoryCoverageScopeAsync(
            tenantId, request.TerritoryNodeId, request.CountryScope, cancellationToken);

        var (items, total, unfilteredTotal) = await _accounts.ListAsync(
            tenantId, request.Search, page, pageSize, request.SortBy, request.SortDir, statuses, accountTypes,
            accountIdScope, cancellationToken);
        var dtos = items.Select(AccountMapper.ToListItem).ToList();

        await EnrichCurrentTerritoryAsync(tenantId, dtos, cancellationToken);

        return Response<PagedResult<AccountListItemDto>>.Success(
            new PagedResult<AccountListItemDto>(dtos, total, page, pageSize, unfilteredTotal));
    }

    /// <summary>Splits a comma-separated chip value (multi-select) into distinct, trimmed, non-empty codes.
    /// Returns null when nothing is selected so the repository skips the predicate entirely.</summary>
    private static IReadOnlyCollection<string>? SplitFilterCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return parts.Length > 0 ? parts : null;
    }

    /// <summary>Splits a comma-separated chip value into distinct, parseable Guids. Unparseable entries are dropped;
    /// returns null when nothing usable is selected.</summary>
    private static IReadOnlyCollection<Guid>? SplitGuidCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => Guid.TryParse(p, out var g) ? g : (Guid?)null)
            .Where(g => g is not null)
            .Select(g => g!.Value)
            .Distinct()
            .ToArray();
        return ids.Length > 0 ? ids : null;
    }

    /// <summary>Resolves the Territory Node / Country Scope chips to the set of CURRENT-coverage account ids to
    /// constrain the account query with. Reuses <see cref="AccountCurrentCoverageResolver"/> (which reuses
    /// <see cref="TerritoryCoverageLifecyclePolicy"/> — the two-gate rule is never reimplemented and the DateTimeOffset
    /// effective-window stays an in-memory check). Within one chip the selected values are a UNION; the two chips are
    /// ANDed together (intersection). Returns null when neither chip is set (predicate skipped); an empty set when a
    /// chip was set but nothing currently qualifies (⇒ zero rows).</summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveTerritoryCoverageScopeAsync(
        Guid tenantId, string? territoryNodeIdCsv, string? countryScopeCsv, CancellationToken cancellationToken)
    {
        var nodeIds = SplitGuidCsv(territoryNodeIdCsv);
        var countryScopes = SplitFilterCsv(countryScopeCsv);
        if (nodeIds is null && countryScopes is null) return null;

        var now = DateTimeOffset.UtcNow;
        HashSet<Guid>? byNode = null;
        HashSet<Guid>? byScope = null;

        if (nodeIds is not null)
        {
            byNode = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
                _territoryAssignments, _territoryModels, tenantId, nodeIds, now, cancellationToken);
        }

        if (countryScopes is not null)
        {
            byScope = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByCountryScopesAsync(
                _territoryAssignments, _territoryModels, tenantId, countryScopes, now, cancellationToken);
        }

        // AND the two chips (each an internal union). When only one chip is set, its set is the scope.
        IEnumerable<Guid> combined = (byNode, byScope) switch
        {
            (not null, not null) => byNode.Intersect(byScope),
            (not null, null) => byNode,
            (null, not null) => byScope,
            _ => []
        };

        return combined.ToList();
    }

    /// <summary>Projects the current (effective-now) MOD-0151 territory coverage onto each list row so the grid can
    /// show it as a column and filter on it. The effective window is filtered in memory (not in Mongo) to avoid the
    /// DateTimeOffset parallel-array pitfall. An account covered by multiple current nodes gets its node names joined.
    ///
    /// <para>FU05A: the grid column is a current-coverage read, so it runs the same
    /// <see cref="TerritoryCoverageLifecyclePolicy"/> gate as CoverageSummary — an assignment whose territory model was
    /// deactivated / archived / superseded stops showing here too, without the assignment row being touched.</para></summary>
    private async Task EnrichCurrentTerritoryAsync(Guid tenantId, List<AccountListItemDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0) return;

        var accountIds = dtos.Select(d => d.Id).ToList();
        var active = await _territoryAssignments.ListActiveByAccountIdsAsync(tenantId, accountIds, cancellationToken);
        if (active.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var open = active.Where(a => TerritoryCoverageLifecyclePolicy.IsAssignmentCurrent(a, now)).ToList();
        if (open.Count == 0) return;

        var models = (await _territoryModels.ListByIdsAsync(
                tenantId, TerritoryCoverageLifecyclePolicy.ModelIdsOf(open), cancellationToken))
            .ToDictionary(m => m.Id);

        var currentByAccount = TerritoryCoverageLifecyclePolicy.FilterCurrent(open, models, now)
            .GroupBy(a => a.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.TerritoryNodeName, StringComparer.OrdinalIgnoreCase).ToList());

        for (var i = 0; i < dtos.Count; i++)
        {
            if (!currentByAccount.TryGetValue(dtos[i].Id, out var nodes) || nodes.Count == 0) continue;

            var primary = nodes[0];
            var name = nodes.Count == 1
                ? primary.TerritoryNodeName
                : string.Join(", ", nodes.Select(n => n.TerritoryNodeName).Where(n => !string.IsNullOrWhiteSpace(n)));

            // Country scope is the owning TerritoryModel's CountryScope (FU02A single-country scope), NOT a node field.
            dtos[i] = dtos[i] with
            {
                TerritoryNodeId = primary.TerritoryNodeId,
                TerritoryNodeCode = primary.TerritoryNodeCode,
                TerritoryNodeName = name,
                TerritoryCountryScope = models.GetValueOrDefault(primary.TerritoryModelId)?.CountryScope
            };
        }
    }
}
