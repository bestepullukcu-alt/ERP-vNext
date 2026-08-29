using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class ListProjectsHandler(ProjectService service) : IRequestHandler<ListProjectsQuery, Response<IReadOnlyList<ProjectDto>>>
{
    public Task<Response<IReadOnlyList<ProjectDto>>> Handle(ListProjectsQuery request, CancellationToken cancellationToken) => service.List(request, cancellationToken);
}
