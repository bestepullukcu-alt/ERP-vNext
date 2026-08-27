using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class ReturnProcessModelToDraftHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ReturnProcessModelToDraftCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(ReturnProcessModelToDraftCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
