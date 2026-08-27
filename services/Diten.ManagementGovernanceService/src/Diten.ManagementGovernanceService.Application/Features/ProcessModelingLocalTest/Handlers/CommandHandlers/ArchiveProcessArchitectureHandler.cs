using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class ArchiveProcessArchitectureHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ArchiveProcessArchitectureCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(ArchiveProcessArchitectureCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
