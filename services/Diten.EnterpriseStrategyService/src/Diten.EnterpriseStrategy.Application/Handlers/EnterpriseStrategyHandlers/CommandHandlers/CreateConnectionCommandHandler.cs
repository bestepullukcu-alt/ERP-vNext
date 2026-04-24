using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateConnectionCommandHandler : IRequestHandler<CreateConnectionCommand, Response<StrategyConnectionDto>>
{
    private readonly IConnectionService _service;

    public CreateConnectionCommandHandler(IConnectionService service) => _service = service;

    public Task<Response<StrategyConnectionDto>> Handle(CreateConnectionCommand request, CancellationToken cancellationToken) =>
        _service.CreateAsync(request.Connection, request.Actor, request.CorrelationId, cancellationToken);
}
