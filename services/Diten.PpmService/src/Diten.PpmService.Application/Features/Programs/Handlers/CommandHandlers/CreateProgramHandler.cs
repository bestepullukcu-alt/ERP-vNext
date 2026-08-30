using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class CreateProgramHandler(ProgramService service) : IRequestHandler<CreateProgramCommand, Response<ProgramDto>>
{
    public Task<Response<ProgramDto>> Handle(CreateProgramCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
