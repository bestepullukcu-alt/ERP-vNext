using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class DeleteInitiativeStrategyLinkCommandHandler : IRequestHandler<DeleteInitiativeStrategyLinkCommand, Response<bool>>
{
    private readonly IInitiativeOrchestrationService _service;

    public DeleteInitiativeStrategyLinkCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<bool>> Handle(DeleteInitiativeStrategyLinkCommand request, CancellationToken cancellationToken) =>
        _service.DeleteStrategyLinkAsync(request.InitiativeId, request.Actor, request.CorrelationId, cancellationToken);
}
