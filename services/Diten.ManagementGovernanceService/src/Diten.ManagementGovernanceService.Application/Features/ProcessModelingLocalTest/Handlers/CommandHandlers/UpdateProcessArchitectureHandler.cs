using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateProcessArchitectureHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateProcessArchitectureCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateProcessArchitectureCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
