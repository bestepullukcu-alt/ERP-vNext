using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateInitiativeCommandHandler : IRequestHandler<UpdateInitiativeCommand, Response<InitiativeStrategyLinkViewDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public UpdateInitiativeCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<InitiativeStrategyLinkViewDto>> Handle(UpdateInitiativeCommand request, CancellationToken cancellationToken) =>
        _service.UpdateAsync(request.InitiativeId, request.Initiative, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
