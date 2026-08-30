using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class SoftDeleteInitiativeHandler(InitiativeService service) : IRequestHandler<SoftDeleteInitiativeCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeleteInitiativeCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
