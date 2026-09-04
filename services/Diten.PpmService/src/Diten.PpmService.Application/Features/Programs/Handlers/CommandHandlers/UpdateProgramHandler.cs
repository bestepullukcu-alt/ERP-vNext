using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class UpdateProgramHandler(ProgramService service) : IRequestHandler<UpdateProgramCommand, Response<ProgramDto>>
{
    public Task<Response<ProgramDto>> Handle(UpdateProgramCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
