using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record CreateStructureCommand(CreateStructureRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<CreateStructureResult>>;
