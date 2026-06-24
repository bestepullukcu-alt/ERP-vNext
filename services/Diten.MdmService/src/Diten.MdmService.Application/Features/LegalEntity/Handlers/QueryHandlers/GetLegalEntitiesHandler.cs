using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntitiesHandler : IRequestHandler<Queries.GetLegalEntitiesQuery, Response<IReadOnlyList<LegalEntityDetailDto>>>
{
    private readonly ILegalEntityRepository _repository;

    public GetLegalEntitiesHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<LegalEntityDetailDto>>> Handle(Queries.GetLegalEntitiesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        IReadOnlyList<LegalEntityDetailDto> items = entities
            .Select(LegalEntityMappings.ToDetailDto)
            .ToList();
        return Response<IReadOnlyList<LegalEntityDetailDto>>.Success(items);
    }
}
