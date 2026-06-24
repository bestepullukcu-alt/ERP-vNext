using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntitiesLookupHandler
    : IRequestHandler<Queries.GetLegalEntitiesLookupQuery, Response<IReadOnlyList<LegalEntityLookupDto>>>
{
    private readonly ILegalEntityRepository _repository;

    public GetLegalEntitiesLookupHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<LegalEntityLookupDto>>> Handle(
        Queries.GetLegalEntitiesLookupQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.GetReferenceableAsync(cancellationToken);
        IReadOnlyList<LegalEntityLookupDto> list = entities.Select(LegalEntityMappings.ToLookupDto).ToList();
        return Response<IReadOnlyList<LegalEntityLookupDto>>.Success(list);
    }
}
