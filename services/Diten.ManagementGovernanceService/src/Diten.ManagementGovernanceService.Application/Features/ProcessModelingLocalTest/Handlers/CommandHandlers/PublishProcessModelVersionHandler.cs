using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class PublishProcessModelVersionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<PublishProcessModelVersionCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(PublishProcessModelVersionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
