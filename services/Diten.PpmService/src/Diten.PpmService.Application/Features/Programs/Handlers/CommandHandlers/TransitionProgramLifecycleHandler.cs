using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class TransitionProgramLifecycleHandler(ProgramService service) : IRequestHandler<TransitionProgramLifecycleCommand, Response<ProgramDto>>
{
    public Task<Response<ProgramDto>> Handle(TransitionProgramLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
