using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Handlers.QueryHandlers;

public sealed class GetGoldenReferenceSlimListHandler : IRequestHandler<GetGoldenReferenceSlimListQuery, Response<IReadOnlyList<GoldenReferenceSlimListItemDto>>>
{
    private readonly IGoldenReferenceSlimRepository _repository;

    public GetGoldenReferenceSlimListHandler(IGoldenReferenceSlimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<GoldenReferenceSlimListItemDto>>> Handle(GetGoldenReferenceSlimListQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        var list = entities.Select(x => new GoldenReferenceSlimListItemDto(
            x.Id,
            x.Code,
            x.Name,
            x.Description,
            x.ReferenceType,
            x.Priority,
            x.IsActive)).ToList();
        return Response<IReadOnlyList<GoldenReferenceSlimListItemDto>>.Success(list);
    }
}
