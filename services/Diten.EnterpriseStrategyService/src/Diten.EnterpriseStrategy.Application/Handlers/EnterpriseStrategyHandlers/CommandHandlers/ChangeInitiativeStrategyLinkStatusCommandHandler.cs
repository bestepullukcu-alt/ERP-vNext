using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeInitiativeStrategyLinkStatusCommandHandler : IRequestHandler<ChangeInitiativeStrategyLinkStatusCommand, Response<InitiativeStrategyLinkViewDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public ChangeInitiativeStrategyLinkStatusCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<InitiativeStrategyLinkViewDto>> Handle(ChangeInitiativeStrategyLinkStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStrategyLinkStatusAsync(request.InitiativeId, request.Status, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
