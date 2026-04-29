using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.QueryHandlers;

public sealed class GetGoldenReferenceCompactByIdHandler : IRequestHandler<GetGoldenReferenceCompactByIdQuery, Response<GoldenReferenceCompactDetailDto>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public GetGoldenReferenceCompactByIdHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GoldenReferenceCompactDetailDto>> Handle(GetGoldenReferenceCompactByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<GoldenReferenceCompactDetailDto>.Fail("Record not found.", 404);
        }

        var dto = new GoldenReferenceCompactDetailDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.ReferenceType,
            entity.Category,
            entity.GroupKey,
            entity.SourceSystem,
            entity.Owner,
            entity.Version,
            entity.EffectiveDate,
            entity.ExpirationDate,
            entity.Priority,
            entity.IsActive);
        return Response<GoldenReferenceCompactDetailDto>.Success(dto);
    }
}
