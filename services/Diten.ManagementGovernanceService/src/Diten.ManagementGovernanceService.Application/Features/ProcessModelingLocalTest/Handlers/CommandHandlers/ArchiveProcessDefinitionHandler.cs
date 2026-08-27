using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class ArchiveProcessDefinitionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ArchiveProcessDefinitionCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(ArchiveProcessDefinitionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
