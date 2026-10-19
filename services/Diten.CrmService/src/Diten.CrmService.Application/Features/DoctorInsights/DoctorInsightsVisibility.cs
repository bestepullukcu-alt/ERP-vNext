using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.DoctorInsights;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.4, K-W7) — the TEAM of a caller: the subtree of every node on which the caller holds a CURRENT
/// <c>territory-subtree</c> resource assignment (assignment active, window covering now, model operationally current — the
/// same gates as <see cref="RepTerritoryCoverage"/> and the <see cref="VisitEvidence.VisitEvidenceRepRoster"/>). CRM has no
/// separate "manager" record: covering a subtree IS leading the reps below it. An exact-territory assignment is the caller's
/// OWN area (<see cref="OwnNodes"/>), never a team. Reads only.
/// </summary>
public sealed class TeamScopeResolver
{
    private readonly ITerritoryResourceAssignmentRepository _assignments;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;

    public TeamScopeResolver(ITerritoryResourceAssignmentRepository assignments, ITerritoryModelRepository models, ITerritoryNodeRepository nodes)
    {
        _assignments = assignments;
        _models = models;
        _nodes = nodes;
    }

    /// <summary>The caller's current assignments (the shared coverage rule).</summary>
    public Task<IReadOnlyList<TerritoryResourceAssignment>> CurrentAsync(Guid tenantId, string resourceId, DateTimeOffset now, CancellationToken ct)
        => RepTerritoryCoverage.CurrentAssignmentsAsync(_assignments, _models, tenantId, resourceId, now, ct);

    /// <summary>The nodes of the caller's exact-territory assignments.</summary>
    public static HashSet<Guid> OwnNodes(IReadOnlyList<TerritoryResourceAssignment> current)
        => current.Where(a => !RepTerritoryCoverage.IsSubtree(a)).Select(a => a.TerritoryId!.Value).ToHashSet();

    /// <summary>The team: each subtree assignment's node + its active descendants.</summary>
    public async Task<HashSet<Guid>> TeamNodesAsync(Guid tenantId, IReadOnlyList<TerritoryResourceAssignment> current, DateTimeOffset now, CancellationToken ct)
    {
        var seeds = current.Where(RepTerritoryCoverage.IsSubtree).Select(a => a.TerritoryId!.Value).Distinct().ToList();
        return seeds.Count == 0
            ? new HashSet<Guid>()
            : (await TerritorySubtree.ExpandAsync(_nodes, tenantId, seeds, now, ct)).ToHashSet();
    }
}

/// <summary>Which doctors a caller sees: everyone (read-all), or the doctors of the own / team nodes.</summary>
public sealed record DoctorScope(bool All, IReadOnlySet<Guid> OwnNodes, IReadOnlySet<Guid> TeamNodes)
{
    public static readonly DoctorScope Nobody = new(false, new HashSet<Guid>(), new HashSet<Guid>());

    public IReadOnlySet<Guid> AllNodes => OwnNodes.Union(TeamNodes).ToHashSet();
}

public static class DoctorScopeKinds
{
    public const string Mine = "mine";
    public const string Team = "team";
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.4) — the visibility of a doctor's insights:
/// <list type="bullet">
/// <item><c>crm.doctor-insights.read-all</c> ⇒ the whole tenant;</item>
/// <item>otherwise the doctors (contacts) ACTIVELY linked to an account whose CURRENT territory coverage is on one of the
/// caller's own (exact) or team (subtree) nodes — the <c>GetMyAccountsQuery</c> chain;</item>
/// <item>no identity or no current assignment ⇒ nobody (unlike "my accounts", an unassigned caller does NOT fall back to
/// the whole tenant: these are a doctor's numbers);</item>
/// <item>a doctor outside the scope answers 404 exactly like a missing one (no leak).</item>
/// </list>
/// </summary>
public sealed class DoctorInsightsVisibility
{
    private readonly ICallerScope _caller;
    private readonly TeamScopeResolver _team;
    private readonly IAccountTerritoryAssignmentRepository _accountAssignments;
    private readonly ITerritoryModelRepository _models;
    private readonly IAccountContactLinkRepository _links;
    private readonly TimeProvider _clock;

    public DoctorInsightsVisibility(ICallerScope caller, TeamScopeResolver team, IAccountTerritoryAssignmentRepository accountAssignments,
        ITerritoryModelRepository models, IAccountContactLinkRepository links, TimeProvider? clock = null)
    {
        _caller = caller;
        _team = team;
        _accountAssignments = accountAssignments;
        _models = models;
        _links = links;
        _clock = clock ?? TimeProvider.System;
    }

    public string? Caller => _caller.CallerResourceId;

    public bool Has(string permission) => _caller.HasPermission(permission);

    public async Task<DoctorScope> ScopeAsync(Guid tenantId, CancellationToken ct)
    {
        if (_caller.HasPermission(DoctorInsightsPermissions.ReadAll)) return new DoctorScope(true, new HashSet<Guid>(), new HashSet<Guid>());
        if (_caller.CallerResourceId is not { } me) return DoctorScope.Nobody;

        var now = _clock.GetUtcNow();
        var current = await _team.CurrentAsync(tenantId, me, now, ct);
        if (current.Count == 0) return DoctorScope.Nobody;
        return new DoctorScope(false, TeamScopeResolver.OwnNodes(current), await _team.TeamNodesAsync(tenantId, current, now, ct));
    }

    /// <summary>The doctors of <paramref name="nodes"/>: active links of the accounts currently covered there.</summary>
    public async Task<HashSet<Guid>> ContactIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> nodes, CancellationToken ct)
    {
        if (nodes.Count == 0) return new HashSet<Guid>();
        var accounts = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
            _accountAssignments, _models, tenantId, nodes, _clock.GetUtcNow(), ct);
        if (accounts.Count == 0) return new HashSet<Guid>();
        return (await _links.ListByAccountIdsAsync(tenantId, accounts, ct))
            .Where(l => l.TenantId == tenantId && RelationshipLifecycle.IsActiveLink(l, contactExists: true))
            .Select(l => l.ContactId)
            .ToHashSet();
    }

    /// <summary>May the caller see this doctor? (read-all, or one of the doctor's linked accounts is covered on a scope node.)</summary>
    public async Task<bool> CanSeeAsync(Guid tenantId, DoctorScope scope, Guid contactId, CancellationToken ct)
    {
        if (scope.All) return true;
        var nodes = scope.AllNodes;
        if (nodes.Count == 0) return false;

        var accounts = (await _links.ListByContactAsync(tenantId, contactId, ct))
            .Where(l => l.TenantId == tenantId && RelationshipLifecycle.IsActiveLink(l, contactExists: true))
            .Select(l => l.AccountId)
            .Distinct()
            .ToList();
        if (accounts.Count == 0) return false;

        var coverage = await AccountCurrentCoverageResolver.ResolveAsync(_accountAssignments, _models, tenantId, accounts, _clock.GetUtcNow(), ct);
        return coverage.Any(c => nodes.Contains(c.TerritoryNodeId));
    }
}
