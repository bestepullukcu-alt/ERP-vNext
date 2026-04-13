using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpsertInitiativeStrategyLinkCommandHandler : IRequestHandler<UpsertInitiativeStrategyLinkCommand, Response<InitiativeStrategyLinkViewDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public UpsertInitiativeStrategyLinkCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<InitiativeStrategyLinkViewDto>> Handle(UpsertInitiativeStrategyLinkCommand request, CancellationToken cancellationToken) =>
        _service.UpsertStrategyLinkAsync(request.InitiativeId, request.Link, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
