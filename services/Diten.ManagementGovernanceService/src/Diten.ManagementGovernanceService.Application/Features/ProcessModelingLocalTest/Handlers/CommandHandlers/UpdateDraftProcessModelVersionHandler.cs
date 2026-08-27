using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Handlers.CommandHandlers;

public sealed class UpdateDraftProcessModelVersionHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<UpdateDraftProcessModelVersionCommand,Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(UpdateDraftProcessModelVersionCommand request,CancellationToken cancellationToken)
        => ProcessModelingLocalHandlerResult.NoContentAsync(executor,request.Execution,cancellationToken);
}
