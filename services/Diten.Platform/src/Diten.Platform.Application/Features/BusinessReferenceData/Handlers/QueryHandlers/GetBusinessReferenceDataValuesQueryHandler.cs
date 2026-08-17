using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataValuesQueryHandler : IRequestHandler<GetBusinessReferenceDataValuesQuery, Response<BusinessReferenceDataValuesLookupModel>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public GetBusinessReferenceDataValuesQueryHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<BusinessReferenceDataValuesLookupModel>> Handle(GetBusinessReferenceDataValuesQuery request, CancellationToken ct)
    {
        var result = await _consumerQueryService.GetValuesAsync(
            request.SetCode,
            request.ScopeKey,
            request.VersionNumber,
            request.AsOfDate,
            request.IncludeDeprecated,
            request.IncludeAttributes,
            request.IncludeMappings,
            ct);
        return Response<BusinessReferenceDataValuesLookupModel>.Success(result);
    }
}
