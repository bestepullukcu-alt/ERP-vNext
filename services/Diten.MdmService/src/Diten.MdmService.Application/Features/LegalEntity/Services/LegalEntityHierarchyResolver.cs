using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Services;

namespace Diten.MdmService.Application.Features.LegalEntity.Services;

public sealed class LegalEntityHierarchyResolver : ILegalEntityHierarchyResolver
{
    private readonly ILegalEntityRepository _repository;

    public LegalEntityHierarchyResolver(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<Guid>?> GetSelfAndDescendantIdsAsync(Guid rootLegalEntityId, CancellationToken cancellationToken = default)
    {
        var all = await _repository.ListAsync(cancellationToken);
        if (all.All(e => e.Id != rootLegalEntityId))
        {
            return null; // root not found / not visible in tenant
        }

        var childrenByParent = all
            .Where(e => e.ParentId.HasValue)
            .GroupBy(e => e.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootLegalEntityId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue; // cycle-safe: never revisit
            }

            result.Add(current);
            if (childrenByParent.TryGetValue(current, out var children))
            {
                foreach (var childId in children)
                {
                    if (!visited.Contains(childId))
                    {
                        queue.Enqueue(childId);
                    }
                }
            }
        }

        return result;
    }
}
