using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class CreateInitiativeHandler(InitiativeService service) : IRequestHandler<CreateInitiativeCommand, Response<InitiativeDto>>
{
    public Task<Response<InitiativeDto>> Handle(CreateInitiativeCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
