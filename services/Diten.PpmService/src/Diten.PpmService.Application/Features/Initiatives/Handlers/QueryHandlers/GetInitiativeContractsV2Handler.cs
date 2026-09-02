using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class GetInitiativeContractsV2Handler(InitiativeService service)
    : IRequestHandler<GetInitiativeContractsV2Query, Response<InitiativeContractsV2>>
{
    public Task<Response<InitiativeContractsV2>> Handle(GetInitiativeContractsV2Query request,
        CancellationToken cancellationToken) => service.GetContracts(cancellationToken);
}
