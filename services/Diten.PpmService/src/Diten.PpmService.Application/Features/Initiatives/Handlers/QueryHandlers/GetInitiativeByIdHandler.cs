using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class GetInitiativeByIdHandler(InitiativeService service) : IRequestHandler<GetInitiativeByIdQuery, Response<InitiativeDto>>
{
    public Task<Response<InitiativeDto>> Handle(GetInitiativeByIdQuery request, CancellationToken cancellationToken) => service.GetById(request, cancellationToken);
}
