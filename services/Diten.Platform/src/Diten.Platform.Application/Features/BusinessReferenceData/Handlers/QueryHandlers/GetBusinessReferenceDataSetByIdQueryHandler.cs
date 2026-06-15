using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataSetByIdQueryHandler : IRequestHandler<GetBusinessReferenceDataSetByIdQuery, Response<BusinessReferenceDataSetDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataSetByIdQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataSetDetailModel>> Handle(GetBusinessReferenceDataSetByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetSetByIdAsync(request.SetId, ct);
        if (entity is null)
        {
            return Response<BusinessReferenceDataSetDetailModel>.Fail("not_found", 404);
        }

        return Response<BusinessReferenceDataSetDetailModel>.Success(new BusinessReferenceDataSetDetailModel(
            entity.BusinessReferenceDataSetId,
            entity.SetCode,
            entity.Name,
            entity.ScopeType,
            entity.Description,
            entity.Status.ToString(),
            entity.ActiveDraftVersionId,
            entity.PublishedVersionId,
            entity.RowVersion,
            entity.CreatedAt,
            entity.UpdatedAt));
    }
}
