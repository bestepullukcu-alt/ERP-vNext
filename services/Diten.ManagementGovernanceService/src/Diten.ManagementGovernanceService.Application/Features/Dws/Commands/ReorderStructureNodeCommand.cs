using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record ReorderStructureNodeCommand(ReorderStructureNodeRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<ReorderStructureNodeResult>>;
