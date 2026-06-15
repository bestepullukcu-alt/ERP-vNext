using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataHierarchyQueryHandler : IRequestHandler<GetBusinessReferenceDataHierarchyQuery, Response<BusinessReferenceDataHierarchyLookupModel>>
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public GetBusinessReferenceDataHierarchyQueryHandler(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<Response<BusinessReferenceDataHierarchyLookupModel>> Handle(GetBusinessReferenceDataHierarchyQuery request, CancellationToken ct)
    {
        var result = await _consumerQueryService.GetHierarchyAsync(
            request.SetCode,
            request.ScopeKey,
            request.VersionNumber,
            request.AsOfDate,
            request.IncludeDeprecated,
            request.IncludeAttributes,
            request.IncludeMappings,
            ct);
        return Response<BusinessReferenceDataHierarchyLookupModel>.Success(result);
    }
}
