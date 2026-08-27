using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateProcessModelHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateProcessModelCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateProcessModelCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
