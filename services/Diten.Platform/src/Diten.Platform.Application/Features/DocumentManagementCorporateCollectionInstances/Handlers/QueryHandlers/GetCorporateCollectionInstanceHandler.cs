using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Handlers.QueryHandlers;

public sealed class GetCorporateCollectionInstanceHandler
    : IRequestHandler<GetCorporateCollectionInstanceQuery, Response<CorporateCollectionInstanceModel>>
{
    private readonly ICollectionInstanceRepository _instances;
    private readonly CorporateCollectionFolderAccessEvaluator _access;

    public GetCorporateCollectionInstanceHandler(
        ICollectionInstanceRepository instances,
        CorporateCollectionFolderAccessEvaluator access)
    {
        _instances = instances;
        _access = access;
    }

    public async Task<Response<CorporateCollectionInstanceModel>> Handle(
        GetCorporateCollectionInstanceQuery request,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.CollectionInstanceId, ct);
        if (instance is null || instance.CollectionScopeType != CollectionScopeType.Corporate)
        {
            return NotFound(request.CorrelationId);
        }
        if (!await _access.HasExplicitGrantAsync(instance.Id, DocumentAccessMatrixAction.View, ct))
        {
            return Response<CorporateCollectionInstanceModel>.Fail(
                "Corporate collection access denied.", 403,
                CorporateCollectionInstanceReasonCodes.Forbidden, request.CorrelationId);
        }

        return Response<CorporateCollectionInstanceModel>.Success(
            CorporateCollectionMapping.ToModel(instance), correlationId: request.CorrelationId);
    }

    private static Response<CorporateCollectionInstanceModel> NotFound(string correlationId) =>
        Response<CorporateCollectionInstanceModel>.Fail(
            "Corporate collection instance not found.", 404,
            CorporateCollectionInstanceReasonCodes.NotFoundNonLeakage, correlationId);
}
