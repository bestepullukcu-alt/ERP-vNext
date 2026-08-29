using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class ListInitiativesHandler(InitiativeService service) : IRequestHandler<ListInitiativesQuery, Response<IReadOnlyList<InitiativeDto>>>
{
    public Task<Response<IReadOnlyList<InitiativeDto>>> Handle(ListInitiativesQuery request, CancellationToken cancellationToken) => service.List(request, cancellationToken);
}
