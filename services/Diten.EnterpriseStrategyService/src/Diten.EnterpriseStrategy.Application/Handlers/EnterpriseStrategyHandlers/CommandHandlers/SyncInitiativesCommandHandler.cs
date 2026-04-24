using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class SyncInitiativesCommandHandler : IRequestHandler<SyncInitiativesCommand, Response<SyncResultDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public SyncInitiativesCommandHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<SyncResultDto>> Handle(SyncInitiativesCommand request, CancellationToken cancellationToken) =>
        _service.SyncAsync(request.CorrelationId, request.Actor, cancellationToken);
}
