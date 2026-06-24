using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.CommandHandlers;

// Re-activates the target node + its descendant subtree (symmetric with archive) and re-activates its
// ancestor chain, so a restored node is never left under an archived parent. Soft status flip only.
public sealed class RestoreCollectionInstanceHandler
    : IRequestHandler<RestoreCollectionInstanceCommand, Response<CollectionInstanceStatusChangeResultModel>>
{
    private readonly ICollectionInstanceRepository _instanceRepository;

    public RestoreCollectionInstanceHandler(ICollectionInstanceRepository instanceRepository)
    {
        _instanceRepository = instanceRepository;
    }

    public async Task<Response<CollectionInstanceStatusChangeResultModel>> Handle(RestoreCollectionInstanceCommand request, CancellationToken ct)
    {
        var target = await _instanceRepository.GetByIdAsync(request.Id, ct);
        if (target is null)
        {
            return Response<CollectionInstanceStatusChangeResultModel>.Fail(
                "Collection instance not found.",
                404,
                DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var group = await _instanceRepository.GetByBaselineAndCompanyAsync(
            target.BaselineReleaseId, target.CompanyId, target.InstanceToken, ct);

        var byCanonicalId = group
            .GroupBy(x => x.CanonicalId)
            .ToDictionary(g => g.Key, g => g.First());
        var childrenByParent = group
            .Where(x => !string.IsNullOrWhiteSpace(x.ParentCanonicalId))
            .GroupBy(x => x.ParentCanonicalId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var affected = new Dictionary<Guid, CollectionInstance>();

        // Downward: the target + its descendant subtree.
        var queue = new Queue<CollectionInstance>();
        queue.Enqueue(target);
        var seen = new HashSet<string>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!seen.Add(node.CanonicalId))
            {
                continue;
            }

            affected[node.Id] = node;
            if (childrenByParent.TryGetValue(node.CanonicalId, out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        // Upward: required ancestors so the restored branch is reachable under active parents.
        var parentId = target.ParentCanonicalId;
        while (!string.IsNullOrWhiteSpace(parentId) && byCanonicalId.TryGetValue(parentId, out var ancestor))
        {
            affected[ancestor.Id] = ancestor;
            parentId = ancestor.ParentCanonicalId;
        }

        var ids = affected.Values
            .Where(x => x.InstanceStatus == CollectionInstanceStatus.Archived)
            .Select(x => x.Id)
            .ToList();
        var restored = ids.Count == 0 ? 0 : await _instanceRepository.ReactivateManyAsync(ids, ct);

        return Response<CollectionInstanceStatusChangeResultModel>.Success(
            new CollectionInstanceStatusChangeResultModel(target.Id, (int)restored),
            correlationId: request.CorrelationId);
    }
}
