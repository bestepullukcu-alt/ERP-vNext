using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class TransitionProjectLifecycleHandler(ProjectService service) : IRequestHandler<TransitionProjectLifecycleCommand, Response<ProjectDto>>
{
    public Task<Response<ProjectDto>> Handle(TransitionProjectLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
