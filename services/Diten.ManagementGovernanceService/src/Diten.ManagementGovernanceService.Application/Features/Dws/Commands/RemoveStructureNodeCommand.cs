using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record RemoveStructureNodeCommand(RemoveStructureNodeRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<RemoveStructureNodeResult>>;
