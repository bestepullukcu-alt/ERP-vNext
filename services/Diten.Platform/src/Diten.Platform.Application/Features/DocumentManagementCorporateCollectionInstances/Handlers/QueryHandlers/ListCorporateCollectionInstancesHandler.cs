using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Handlers.QueryHandlers;

public sealed class ListCorporateCollectionInstancesHandler
    : IRequestHandler<ListCorporateCollectionInstancesQuery, Response<IReadOnlyList<CorporateCollectionInstanceModel>>>
{
    private readonly ICollectionInstanceRepository _instances;
    private readonly CorporateCollectionFolderAccessEvaluator _access;

    public ListCorporateCollectionInstancesHandler(
        ICollectionInstanceRepository instances,
        CorporateCollectionFolderAccessEvaluator access)
    {
        _instances = instances;
        _access = access;
    }

    public async Task<Response<IReadOnlyList<CorporateCollectionInstanceModel>>> Handle(
        ListCorporateCollectionInstancesQuery request,
        CancellationToken ct)
    {
        var candidates = await _instances.GetCorporateAsync(request.BaselineReleaseId, request.CorporateOwnerId, ct);
        var visible = new List<CorporateCollectionInstanceModel>();
        foreach (var candidate in candidates.Where(x => x.InstanceStatus != CollectionInstanceStatus.Archived))
        {
            if (await _access.HasExplicitGrantAsync(candidate.Id, DocumentAccessMatrixAction.View, ct))
            {
                visible.Add(CorporateCollectionMapping.ToModel(candidate));
            }
        }

        return Response<IReadOnlyList<CorporateCollectionInstanceModel>>.Success(
            visible, correlationId: request.CorrelationId);
    }
}
