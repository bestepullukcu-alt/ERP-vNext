using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntityByIdHandler : IRequestHandler<Queries.GetLegalEntityByIdQuery, Response<LegalEntityDetailDto>>
{
    private readonly ILegalEntityRepository _repository;

    public GetLegalEntityByIdHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<LegalEntityDetailDto>> Handle(Queries.GetLegalEntityByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        return entity is null
            ? Response<LegalEntityDetailDto>.Fail("Legal Entity not found.", 404)
            : Response<LegalEntityDetailDto>.Success(LegalEntityMappings.ToDetailDto(entity));
    }
}
