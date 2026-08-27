using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class CreateProcessFamilyHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<CreateProcessFamilyCommand,Response<Guid>>
{
    public Task<Response<Guid>> Handle(CreateProcessFamilyCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.CreateAsync(executor,request.Execution,cancellationToken);
}
