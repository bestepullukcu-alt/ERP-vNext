using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class ArchiveProcessDomainHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ArchiveProcessDomainCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(ArchiveProcessDomainCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
