using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record MoveStructureNodeCommand(MoveStructureNodeRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<MoveStructureNodeResult>>;
