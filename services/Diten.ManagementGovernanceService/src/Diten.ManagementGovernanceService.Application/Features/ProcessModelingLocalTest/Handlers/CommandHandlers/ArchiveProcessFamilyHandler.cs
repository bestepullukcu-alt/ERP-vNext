using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class ArchiveProcessFamilyHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ArchiveProcessFamilyCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(ArchiveProcessFamilyCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
