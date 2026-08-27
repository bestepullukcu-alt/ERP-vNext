using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessArchitectureHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessArchitectureCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessArchitectureCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
