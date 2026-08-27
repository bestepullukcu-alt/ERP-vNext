using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessModelRevisionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessModelRevisionCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessModelRevisionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
