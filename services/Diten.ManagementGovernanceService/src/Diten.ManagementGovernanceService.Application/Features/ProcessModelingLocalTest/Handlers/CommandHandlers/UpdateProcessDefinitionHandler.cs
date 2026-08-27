using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateProcessDefinitionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateProcessDefinitionCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateProcessDefinitionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
