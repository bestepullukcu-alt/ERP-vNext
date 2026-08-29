using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateProcessDomainHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateProcessDomainCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateProcessDomainCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
