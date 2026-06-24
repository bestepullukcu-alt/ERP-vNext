using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;

public sealed class GetCollectionInstanceByIdHandler
    : IRequestHandler<GetCollectionInstanceByIdQuery, Response<CollectionInstanceDetailModel>>
{
    private readonly ICollectionInstanceRepository _instanceRepository;

    public GetCollectionInstanceByIdHandler(ICollectionInstanceRepository instanceRepository)
    {
        _instanceRepository = instanceRepository;
    }

    public async Task<Response<CollectionInstanceDetailModel>> Handle(GetCollectionInstanceByIdQuery request, CancellationToken ct)
    {
        var instance = await _instanceRepository.GetByIdAsync(request.Id, ct);
        if (instance is null)
        {
            return Response<CollectionInstanceDetailModel>.Fail(
                "Collection instance not found.",
                404,
                DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        return Response<CollectionInstanceDetailModel>.Success(
            InstantiationMapping.ToDetail(instance),
            correlationId: request.CorrelationId);
    }
}
