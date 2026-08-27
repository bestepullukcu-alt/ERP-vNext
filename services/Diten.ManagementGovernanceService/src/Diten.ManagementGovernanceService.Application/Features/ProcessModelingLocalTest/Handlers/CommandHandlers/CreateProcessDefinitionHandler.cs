using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessDefinitionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessDefinitionCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessDefinitionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
