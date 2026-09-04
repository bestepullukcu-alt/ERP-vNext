using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class ListInitiativesHandler(InitiativeService service) : IRequestHandler<ListInitiativesQuery, Response<IReadOnlyList<InitiativeV2Dto>>>
{
    public Task<Response<IReadOnlyList<InitiativeV2Dto>>> Handle(ListInitiativesQuery request, CancellationToken cancellationToken) => service.List(request, cancellationToken);
}
