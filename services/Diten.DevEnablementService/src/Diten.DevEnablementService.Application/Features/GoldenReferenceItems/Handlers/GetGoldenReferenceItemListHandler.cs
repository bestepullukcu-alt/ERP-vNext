using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class GetGoldenReferenceItemListHandler : IRequestHandler<GetGoldenReferenceItemListQuery, Response<IReadOnlyList<GoldenReferenceItemListItemDto>>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public GetGoldenReferenceItemListHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<GoldenReferenceItemListItemDto>>> Handle(GetGoldenReferenceItemListQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        var list = entities.Select(x => new GoldenReferenceItemListItemDto(x.Id, x.Code, x.Name, x.ReferenceType, x.Priority, x.IsActive)).ToList();
        return Response<IReadOnlyList<GoldenReferenceItemListItemDto>>.Success(list);
    }
}
