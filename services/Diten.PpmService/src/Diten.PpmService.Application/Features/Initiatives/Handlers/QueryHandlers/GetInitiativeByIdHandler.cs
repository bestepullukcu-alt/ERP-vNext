using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class GetInitiativeByIdHandler(InitiativeService service) : IRequestHandler<GetInitiativeByIdQuery, Response<InitiativeV2Dto>>
{
    public Task<Response<InitiativeV2Dto>> Handle(GetInitiativeByIdQuery request, CancellationToken cancellationToken) => service.GetById(request, cancellationToken);
}
