using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record AddStructureNodeCommand(AddStructureNodeRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<AddStructureNodeResult>>;
