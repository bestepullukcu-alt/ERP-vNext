using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class GetProgramByIdHandler(ProgramService service) : IRequestHandler<GetProgramByIdQuery, Response<ProgramDto>>
{
    public Task<Response<ProgramDto>> Handle(GetProgramByIdQuery request, CancellationToken cancellationToken) => service.GetById(request, cancellationToken);
}
