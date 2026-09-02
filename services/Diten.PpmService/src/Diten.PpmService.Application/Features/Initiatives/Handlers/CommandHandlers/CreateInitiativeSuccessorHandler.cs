using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class CreateInitiativeSuccessorHandler(InitiativeService service)
    : IRequestHandler<CreateInitiativeSuccessorCommand, Response<InitiativeV2Dto>>
{
    public Task<Response<InitiativeV2Dto>> Handle(CreateInitiativeSuccessorCommand request,
        CancellationToken cancellationToken) => service.CreateSuccessor(request, cancellationToken);
}
