using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class ValidateLegalEntityReferenceHandler : IRequestHandler<Queries.ValidateLegalEntityReferenceQuery, Response<LegalEntityLookupDto>>
{
    private readonly ILegalEntityRepository _repository;

    public ValidateLegalEntityReferenceHandler(ILegalEntityRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<LegalEntityLookupDto>> Handle(Queries.ValidateLegalEntityReferenceQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.LegalEntityId, cancellationToken);
        if (entity is null || entity.LifecycleStatus != LegalEntityLifecycleStatus.Active)
        {
            return Response<LegalEntityLookupDto>.Fail("Legal Entity is not referenceable.", 404);
        }

        return Response<LegalEntityLookupDto>.Success(LegalEntityMappings.ToLookupDto(entity));
    }
}
