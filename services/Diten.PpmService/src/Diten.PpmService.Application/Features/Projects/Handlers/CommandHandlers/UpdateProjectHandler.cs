using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class UpdateProjectHandler(ProjectService service) : IRequestHandler<UpdateProjectCommand, Response<ProjectDto>>
{
    public Task<Response<ProjectDto>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
