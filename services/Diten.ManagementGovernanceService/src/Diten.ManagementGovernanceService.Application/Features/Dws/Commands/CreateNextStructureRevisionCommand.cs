using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record CreateNextStructureRevisionCommand(CreateNextStructureRevisionRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<CreateNextStructureRevisionResult>>;
