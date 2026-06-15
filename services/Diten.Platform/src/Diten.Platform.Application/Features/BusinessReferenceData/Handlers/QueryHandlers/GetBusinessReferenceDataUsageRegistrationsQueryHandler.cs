using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataUsageRegistrationsQueryHandler : IRequestHandler<GetBusinessReferenceDataUsageRegistrationsQuery, Response<BusinessReferenceDataUsageRegistrationListModel>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public GetBusinessReferenceDataUsageRegistrationsQueryHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<BusinessReferenceDataUsageRegistrationListModel>> Handle(GetBusinessReferenceDataUsageRegistrationsQuery request, CancellationToken ct)
    {
        var result = await _consumerQueryService.GetUsageRegistrationsAsync(request.SetCode, ct);
        return Response<BusinessReferenceDataUsageRegistrationListModel>.Success(result);
    }
}
