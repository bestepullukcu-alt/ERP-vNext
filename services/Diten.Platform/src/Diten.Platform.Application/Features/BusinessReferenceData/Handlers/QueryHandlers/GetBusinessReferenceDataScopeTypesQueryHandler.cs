using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataScopeTypesQueryHandler
    : IRequestHandler<GetBusinessReferenceDataScopeTypesQuery, Response<IReadOnlyList<BusinessReferenceDataScopeTypeModel>>>
{
    public Task<Response<IReadOnlyList<BusinessReferenceDataScopeTypeModel>>> Handle(
        GetBusinessReferenceDataScopeTypesQuery request,
        CancellationToken ct)
    {
        return Task.FromResult(
            Response<IReadOnlyList<BusinessReferenceDataScopeTypeModel>>.Success(BusinessReferenceDataScopeTypes.Options));
    }
}
