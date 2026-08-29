using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class SoftDeleteProgramHandler(ProgramService service) : IRequestHandler<SoftDeleteProgramCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeleteProgramCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
