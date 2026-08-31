using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class SoftDeleteProjectHandler(ProjectService service) : IRequestHandler<SoftDeleteProjectCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeleteProjectCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
