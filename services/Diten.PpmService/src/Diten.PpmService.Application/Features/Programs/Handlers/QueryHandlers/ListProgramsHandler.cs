using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class ListProgramsHandler(ProgramService service) : IRequestHandler<ListProgramsQuery, Response<IReadOnlyList<ProgramDto>>>
{
    public Task<Response<IReadOnlyList<ProgramDto>>> Handle(ListProgramsQuery request, CancellationToken cancellationToken) => service.List(request, cancellationToken);
}
