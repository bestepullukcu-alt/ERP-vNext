using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessModelHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessModelCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessModelCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
