using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateProcessFamilyHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateProcessFamilyCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateProcessFamilyCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
