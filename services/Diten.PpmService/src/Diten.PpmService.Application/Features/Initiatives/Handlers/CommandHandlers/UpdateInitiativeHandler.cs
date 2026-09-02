using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class UpdateInitiativeHandler(InitiativeService service) : IRequestHandler<UpdateInitiativeCommand, Response<InitiativeV2Dto>>
{
    public Task<Response<InitiativeV2Dto>> Handle(UpdateInitiativeCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
