using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record RemoveStructuralDependencyCommand(RemoveStructuralDependencyRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<RemoveStructuralDependencyResult>>;
