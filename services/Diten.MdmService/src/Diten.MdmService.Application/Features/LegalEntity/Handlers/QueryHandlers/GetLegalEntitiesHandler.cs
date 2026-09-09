using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntitiesHandler : IRequestHandler<Queries.GetLegalEntitiesQuery, Response<IReadOnlyList<LegalEntityListItemDto>>>
{
    private readonly ILegalEntityRepository _repository;

    public GetLegalEntitiesHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<LegalEntityListItemDto>>> Handle(Queries.GetLegalEntitiesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(cancellationToken);
        IReadOnlyList<LegalEntityListItemDto> items = entities.Select(LegalEntityMappings.ToListItemDto).ToList();
        return Response<IReadOnlyList<LegalEntityListItemDto>>.Success(items);
    }
}
