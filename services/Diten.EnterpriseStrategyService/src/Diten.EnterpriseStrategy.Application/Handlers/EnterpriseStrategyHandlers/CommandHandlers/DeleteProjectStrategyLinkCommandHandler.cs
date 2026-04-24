using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class DeleteProjectStrategyLinkCommandHandler : IRequestHandler<DeleteProjectStrategyLinkCommand, Response<bool>>
{
    private readonly IProjectOrchestrationService _service;

    public DeleteProjectStrategyLinkCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<bool>> Handle(DeleteProjectStrategyLinkCommand request, CancellationToken cancellationToken) =>
        _service.DeleteStrategyLinkAsync(request.ProjectId, request.Actor, request.CorrelationId, cancellationToken);
}
