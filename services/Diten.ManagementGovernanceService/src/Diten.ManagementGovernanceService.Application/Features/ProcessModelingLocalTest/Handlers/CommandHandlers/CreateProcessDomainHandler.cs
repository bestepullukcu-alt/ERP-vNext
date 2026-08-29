using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessDomainHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessDomainCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessDomainCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
