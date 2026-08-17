using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateConnectionCommandHandler : IRequestHandler<UpdateConnectionCommand, Response<StrategyConnectionDto>>
{
    private readonly IConnectionService _service;

    public UpdateConnectionCommandHandler(IConnectionService service) => _service = service;

    public Task<Response<StrategyConnectionDto>> Handle(UpdateConnectionCommand request, CancellationToken cancellationToken) =>
        _service.UpdateAsync(request.ConnectionId, request.Connection, request.ExpectedVersion, request.Actor, request.CorrelationId, cancellationToken);
}
