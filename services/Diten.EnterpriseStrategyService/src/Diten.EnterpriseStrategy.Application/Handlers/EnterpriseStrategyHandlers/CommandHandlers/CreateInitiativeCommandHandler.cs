using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateInitiativeCommandHandler : IRequestHandler<CreateInitiativeCommand, Response<InitiativeStrategyLinkViewDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public CreateInitiativeCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<InitiativeStrategyLinkViewDto>> Handle(CreateInitiativeCommand request, CancellationToken cancellationToken) =>
        _service.CreateAsync(request.Initiative, request.Actor, request.CorrelationId, cancellationToken);
}
