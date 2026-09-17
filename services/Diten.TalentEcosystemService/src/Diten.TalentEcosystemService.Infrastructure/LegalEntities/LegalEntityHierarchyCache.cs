using Diten.TalentEcosystemService.Application.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diten.TalentEcosystemService.Infrastructure.LegalEntities;

/// <summary>
/// Resolves legal-entity descendants from the MDM hierarchy, caching the parent→children adjacency in
/// memory for a short TTL so scoping does not call MDM on every request. Cycle-safe. Fail-safe: when
/// the hierarchy cannot be loaded, an id resolves to itself only (never to a broader set).
/// </summary>
public sealed class LegalEntityHierarchyCache : ILegalEntityHierarchyCache
{
    private const string CacheKey = "hcm:legal-entity:hierarchy:children-adjacency";

    private readonly IMemoryCache _cache;
    private readonly MdmLegalEntityHierarchyClient _client;
    private readonly TimeSpan _ttl;

    public LegalEntityHierarchyCache(
        IMemoryCache cache,
        MdmLegalEntityHierarchyClient client,
        IOptions<MdmServiceOptions> options)
    {
        _cache = cache;
        _client = client;
        var seconds = options.Value.CacheSeconds > 0 ? options.Value.CacheSeconds : 60;
        _ttl = TimeSpan.FromSeconds(seconds);
    }

    public async Task<IReadOnlyCollection<Guid>> GetSelfAndDescendantsAsync(Guid legalEntityId, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var children = await GetChildrenAdjacencyAsync(ct);

        var result = new HashSet<Guid> { legalEntityId };
        var queue = new Queue<Guid>();
        queue.Enqueue(legalEntityId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!children.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var child in next)
            {
                if (result.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, List<Guid>>> GetChildrenAdjacencyAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<Guid, List<Guid>>? cached) && cached is not null)
        {
            return cached;
        }

        var edges = await _client.GetEdgesAsync(ct);
        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges)
        {
            if (edge.ParentId is { } parent && parent != Guid.Empty && parent != edge.Id)
            {
                if (!adjacency.TryGetValue(parent, out var list))
                {
                    list = new List<Guid>();
                    adjacency[parent] = list;
                }

                list.Add(edge.Id);
            }
        }

        // Cache even an empty adjacency briefly so a transient MDM outage does not stampede.
        _cache.Set(CacheKey, (IReadOnlyDictionary<Guid, List<Guid>>)adjacency, _ttl);
        return adjacency;
    }
}
