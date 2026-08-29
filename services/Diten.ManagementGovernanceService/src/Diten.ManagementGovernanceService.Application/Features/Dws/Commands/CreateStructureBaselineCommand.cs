using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record CreateStructureBaselineCommand(CreateStructureBaselineRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<CreateStructureBaselineResult>>;
