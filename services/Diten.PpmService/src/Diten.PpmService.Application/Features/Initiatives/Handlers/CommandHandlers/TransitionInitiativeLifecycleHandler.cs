using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class TransitionInitiativeLifecycleHandler(InitiativeService service) : IRequestHandler<TransitionInitiativeLifecycleCommand, Response<InitiativeLifecycleResult>>
{
    public Task<Response<InitiativeLifecycleResult>> Handle(TransitionInitiativeLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
