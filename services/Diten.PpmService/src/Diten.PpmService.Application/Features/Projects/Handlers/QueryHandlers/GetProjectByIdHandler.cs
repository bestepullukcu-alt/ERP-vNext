using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class GetProjectByIdHandler(ProjectService service) : IRequestHandler<GetProjectByIdQuery, Response<ProjectDto>>
{
    public Task<Response<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken) => service.GetById(request, cancellationToken);
}
