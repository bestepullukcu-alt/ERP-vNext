using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

public sealed record AddStructuralDependencyCommand(AddStructuralDependencyRequest Request, DwsTrustedActorContext Context)
    : IRequest<Response<AddStructuralDependencyResult>>;
