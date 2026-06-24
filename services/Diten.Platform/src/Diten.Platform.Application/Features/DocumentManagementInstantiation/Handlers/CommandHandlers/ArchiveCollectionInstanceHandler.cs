using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.CommandHandlers;

// Archives the target node + its descendants (within the same company/baseline/token group) so a container
// is never left with active children. Soft only (InstanceStatus = Archived); never a hard delete.
public sealed class ArchiveCollectionInstanceHandler
    : IRequestHandler<ArchiveCollectionInstanceCommand, Response<CollectionInstanceStatusChangeResultModel>>
{
    private readonly ICollectionInstanceRepository _instanceRepository;

    public ArchiveCollectionInstanceHandler(ICollectionInstanceRepository instanceRepository)
    {
        _instanceRepository = instanceRepository;
    }

    public async Task<Response<CollectionInstanceStatusChangeResultModel>> Handle(ArchiveCollectionInstanceCommand request, CancellationToken ct)
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

        // Resolve the company/baseline/token group, then walk the canonical-id tree down from the target.
        var group = await _instanceRepository.GetByBaselineAndCompanyAsync(
            target.BaselineReleaseId, target.CompanyId, target.InstanceToken, ct);

        var childrenByParent = group
            .Where(x => !string.IsNullOrWhiteSpace(x.ParentCanonicalId))
            .GroupBy(x => x.ParentCanonicalId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var subtree = new List<CollectionInstance>();
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

            subtree.Add(node);
            if (childrenByParent.TryGetValue(node.CanonicalId, out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        var ids = subtree
            .Where(x => x.InstanceStatus != CollectionInstanceStatus.Archived)
            .Select(x => x.Id)
            .ToList();
        var archived = ids.Count == 0 ? 0 : await _instanceRepository.ArchiveManyAsync(ids, ct);

        return Response<CollectionInstanceStatusChangeResultModel>.Success(
            new CollectionInstanceStatusChangeResultModel(target.Id, (int)archived),
            correlationId: request.CorrelationId);
    }
}
