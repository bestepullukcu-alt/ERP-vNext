using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;

/// <summary>
/// WP-VP-2 (B-2) rule, shared since WP-VP-2B — which territory a rep covers RIGHT NOW: their resource assignments that
/// are active, not deleted, carry a node, whose validity window covers now and whose owning model is operationally current
/// (<see cref="TerritoryCoverageLifecyclePolicy"/>); <c>exact-territory</c> covers the node, <c>territory-subtree</c> the
/// node and its active descendants (<see cref="TerritorySubtree"/>). Used by "my accounts" and the account filter options,
/// so the two can never disagree. Reads only.
/// </summary>
public static class RepTerritoryCoverage
{
    public static async Task<IReadOnlyList<TerritoryResourceAssignment>> CurrentAssignmentsAsync(
        ITerritoryResourceAssignmentRepository resourceAssignments,
        ITerritoryModelRepository models,
        Guid tenantId,
        string resourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mine = (await resourceAssignments.ListByResourceAsync(tenantId, resourceId, cancellationToken))
            .Where(a => !a.IsDeleted
                        && a.TerritoryId is { } t && t != Guid.Empty
                        && string.Equals(a.Status, "active", StringComparison.OrdinalIgnoreCase)
                        && a.ValidFrom <= now
                        && (a.ValidTo is null || a.ValidTo >= now))
            .ToList();
        if (mine.Count == 0) return mine;

        var modelMap = (await models.ListByIdsAsync(tenantId, mine.Select(a => a.ModelId).Distinct().ToList(), cancellationToken))
            .ToDictionary(m => m.Id);
        return mine.Where(a => TerritoryCoverageLifecyclePolicy.IsModelCurrent(modelMap.GetValueOrDefault(a.ModelId), now)).ToList();
    }

    /// <summary>The nodes the current assignments cover (exact ⇒ the node; subtree ⇒ node + active descendants).</summary>
    public static async Task<HashSet<Guid>> CoveredNodesAsync(
        ITerritoryNodeRepository nodes,
        Guid tenantId,
        IReadOnlyList<TerritoryResourceAssignment> current,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var covered = new HashSet<Guid>(current.Where(a => !IsSubtree(a)).Select(a => a.TerritoryId!.Value));
        var subtree = current.Where(IsSubtree).Select(a => a.TerritoryId!.Value).ToList();
        if (subtree.Count > 0)
        {
            covered.UnionWith(await TerritorySubtree.ExpandAsync(nodes, tenantId, subtree, now, cancellationToken));
        }

        return covered;
    }

    public static bool IsSubtree(TerritoryResourceAssignment a)
        => string.Equals(a.CoverageScope, TerritoryCoverageScopes.TerritorySubtree, StringComparison.OrdinalIgnoreCase);
}
