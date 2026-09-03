using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class GetInitiativeDetailLinksHandler(InitiativeService service)
    : IRequestHandler<GetInitiativeDetailLinksQuery, Response<InitiativeDetailLinks>>
{
    public Task<Response<InitiativeDetailLinks>> Handle(GetInitiativeDetailLinksQuery request,
        CancellationToken cancellationToken) => service.GetDetailLinks(request, cancellationToken);
}
