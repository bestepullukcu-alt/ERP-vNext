using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class TransitionInitiativeLifecycleHandler(InitiativeService service) : IRequestHandler<TransitionInitiativeLifecycleCommand, Response<InitiativeDto>>
{
    public Task<Response<InitiativeDto>> Handle(TransitionInitiativeLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
