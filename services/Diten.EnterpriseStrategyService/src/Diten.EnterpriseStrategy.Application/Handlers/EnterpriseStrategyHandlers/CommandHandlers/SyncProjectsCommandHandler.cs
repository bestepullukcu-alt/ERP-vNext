using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class SyncProjectsCommandHandler : IRequestHandler<SyncProjectsCommand, Response<SyncResultDto>>
{
    private readonly IProjectOrchestrationService _service;

    public SyncProjectsCommandHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<SyncResultDto>> Handle(SyncProjectsCommand request, CancellationToken cancellationToken) =>
        _service.SyncAsync(request.CorrelationId, request.Actor, cancellationToken);
}
