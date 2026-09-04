using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class CreateInitiativeHandler(InitiativeService service) : IRequestHandler<CreateInitiativeCommand, Response<InitiativeV2Dto>>
{
    public Task<Response<InitiativeV2Dto>> Handle(CreateInitiativeCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
