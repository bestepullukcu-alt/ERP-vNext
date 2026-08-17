using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataPublishedValuesQueryHandler : IRequestHandler<GetBusinessReferenceDataPublishedValuesQuery, Response<BusinessReferenceDataPublishedValuesModel>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public GetBusinessReferenceDataPublishedValuesQueryHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<BusinessReferenceDataPublishedValuesModel>> Handle(GetBusinessReferenceDataPublishedValuesQuery request, CancellationToken ct)
    {
        var result = await _consumerQueryService.GetPublishedValuesAsync(request.SetCode, request.ScopeKey, ct);
        return Response<BusinessReferenceDataPublishedValuesModel>.Success(result);
    }
}
