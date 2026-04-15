using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangeStrategyPeriodStatusCommandHandler : IRequestHandler<ChangeStrategyPeriodStatusCommand, Response<StrategyPeriodDto>>
{
    private readonly IPlanningCycleService _service;

    public ChangeStrategyPeriodStatusCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodDto>> Handle(ChangeStrategyPeriodStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangeStrategyPeriodStatusAsync(request.StrategyPeriodId, request.Status, request.Actor, cancellationToken);
}
