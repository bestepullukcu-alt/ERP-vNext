using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeConnectionStatusCommandHandler : IRequestHandler<ChangeConnectionStatusCommand, Response<StrategyConnectionDto>>
{
    private readonly IConnectionService _service;

    public ChangeConnectionStatusCommandHandler(IConnectionService service) => _service = service;

    public Task<Response<StrategyConnectionDto>> Handle(ChangeConnectionStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStatusAsync(request.ConnectionId, request.Status, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
