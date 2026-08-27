using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class RetireProcessModelVersionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<RetireProcessModelVersionCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(RetireProcessModelVersionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
