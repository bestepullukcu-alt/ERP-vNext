using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class GetInitiativeLifecycleContractsV2Handler(InitiativeService service)
    : IRequestHandler<GetInitiativeLifecycleContractsV2Query, Response<InitiativeLifecycleContractsV2>>
{
    public Task<Response<InitiativeLifecycleContractsV2>> Handle(
        GetInitiativeLifecycleContractsV2Query request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.GetLifecycleContracts(cancellationToken);
    }
}
