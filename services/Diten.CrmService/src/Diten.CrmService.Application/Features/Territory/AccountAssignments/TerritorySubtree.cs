using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.AccountAssignments;

/// <summary>
/// WP-VP-2 (B-2) — a territory node's subtree: the node itself plus every ACTIVE descendant (status <c>active</c>, its
/// own effective window covering the instant) in the same model, walked through <c>ParentTerritoryId</c>. A descendant
/// that is not active cuts its branch off. The given nodes are always part of the answer (as the exact-node filter
/// treated them). Reads one node list per model; never writes.
/// </summary>
public static class TerritorySubtree
{
    public static async Task<IReadOnlyCollection<Guid>> ExpandAsync(
        ITerritoryNodeRepository nodes,
        Guid tenantId,
        IReadOnlyCollection<Guid> nodeIds,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid>(nodeIds.Where(id => id != Guid.Empty));
        if (result.Count == 0) return result;

        var seeds = await nodes.ListByIdsAsync(tenantId, result.ToList(), cancellationToken);
        foreach (var modelId in seeds.Select(n => n.ModelId).Distinct())
        {
            var children = (await nodes.ListByModelAsync(tenantId, modelId, cancellationToken))
                .Where(n => n.ParentTerritoryId is not null && IsActive(n, at))
                .GroupBy(n => n.ParentTerritoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

            var queue = new Queue<Guid>(seeds.Where(n => n.ModelId == modelId).Select(n => n.Id));
            while (queue.Count > 0)
            {
                if (!children.TryGetValue(queue.Dequeue(), out var kids)) continue;
                foreach (var kid in kids)
                {
                    if (result.Add(kid)) queue.Enqueue(kid);
                }
            }
        }

        return result;
    }

    private static bool IsActive(TerritoryNode node, DateTimeOffset at)
        => !node.IsDeleted
           && string.Equals(node.Status, "active", StringComparison.OrdinalIgnoreCase)
           && node.EffectiveFrom <= at
           && (node.EffectiveTo is null || node.EffectiveTo >= at);
}
