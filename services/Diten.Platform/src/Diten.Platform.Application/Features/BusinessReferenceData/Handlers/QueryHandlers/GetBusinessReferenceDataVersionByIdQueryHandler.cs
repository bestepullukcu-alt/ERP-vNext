using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataVersionByIdQueryHandler : IRequestHandler<GetBusinessReferenceDataVersionByIdQuery, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataVersionByIdQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(GetBusinessReferenceDataVersionByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (entity is null)
        {
            return Response<BusinessReferenceDataVersionDetailModel>.Fail("not_found", 404);
        }

        return Response<BusinessReferenceDataVersionDetailModel>.Success(BusinessReferenceDataModelMapper.ToVersionDetail(entity));
    }
}
