using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class CreateProjectHandler(ProjectService service) : IRequestHandler<CreateProjectCommand, Response<ProjectDto>>
{
    public Task<Response<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
