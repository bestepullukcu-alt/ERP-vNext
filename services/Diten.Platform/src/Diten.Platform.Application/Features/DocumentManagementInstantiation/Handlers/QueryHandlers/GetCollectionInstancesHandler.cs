using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;

public sealed class GetCollectionInstancesHandler
    : IRequestHandler<GetCollectionInstancesQuery, Response<IReadOnlyList<CollectionInstanceListItemModel>>>
{
    private readonly ICollectionInstanceRepository _instanceRepository;

    public GetCollectionInstancesHandler(ICollectionInstanceRepository instanceRepository)
    {
        _instanceRepository = instanceRepository;
    }

    public async Task<Response<IReadOnlyList<CollectionInstanceListItemModel>>> Handle(GetCollectionInstancesQuery request, CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.DocumentManagement.CollectionInstance> instances;
        if (request.CompanyId.HasValue && request.BaselineReleaseId.HasValue)
        {
            instances = await _instanceRepository.GetByBaselineAndCompanyAsync(
                request.BaselineReleaseId.Value,
                request.CompanyId.Value,
                request.InstanceToken,
                ct);
        }
        else if (request.CompanyId.HasValue)
        {
            instances = await _instanceRepository.GetByCompanyAsync(request.CompanyId.Value, ct);
        }
        else
        {
            instances = await _instanceRepository.GetAllForTenantAsync(ct);
        }

        return Response<IReadOnlyList<CollectionInstanceListItemModel>>.Success(
            instances.Select(InstantiationMapping.ToListItem).ToList(),
            correlationId: request.CorrelationId);
    }
}
