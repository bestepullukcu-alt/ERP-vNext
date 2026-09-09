using Diten.MdmService.Domain.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;

public sealed class GetLegalEntityDescendantsHandler : IRequestHandler<Queries.GetLegalEntityDescendantsQuery, Response<LegalEntityDescendantsDto>>
{
    private readonly ILegalEntityHierarchyResolver _hierarchyResolver;

    public GetLegalEntityDescendantsHandler(ILegalEntityHierarchyResolver hierarchyResolver)
    {
        _hierarchyResolver = hierarchyResolver;
    }

    public async Task<Response<LegalEntityDescendantsDto>> Handle(Queries.GetLegalEntityDescendantsQuery request, CancellationToken cancellationToken)
    {
        var ids = await _hierarchyResolver.GetSelfAndDescendantIdsAsync(request.LegalEntityId, cancellationToken);
        return ids is null
            ? Response<LegalEntityDescendantsDto>.Fail("Legal Entity not found.", 404)
            : Response<LegalEntityDescendantsDto>.Success(new LegalEntityDescendantsDto(request.LegalEntityId, ids));
    }
}
