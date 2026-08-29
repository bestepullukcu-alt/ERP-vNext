using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class UpdateInitiativeHandler(InitiativeService service) : IRequestHandler<UpdateInitiativeCommand, Response<InitiativeDto>>
{
    public Task<Response<InitiativeDto>> Handle(UpdateInitiativeCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
