using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.QueryHandlers;

public sealed class GetGoldenReferenceCompactListHandler : IRequestHandler<GetGoldenReferenceCompactListQuery, Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public GetGoldenReferenceCompactListHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>> Handle(GetGoldenReferenceCompactListQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        var list = entities.Select(x => new GoldenReferenceCompactListItemDto(
            x.Id,
            x.Code,
            x.Name,
            x.Description,
            x.ReferenceType,
            x.Category,
            x.GroupKey,
            x.SourceSystem,
            x.Owner,
            x.Version,
            x.EffectiveDate,
            x.ExpirationDate,
            x.Priority,
            x.IsActive)).ToList();
        return Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>.Success(list);
    }
}
