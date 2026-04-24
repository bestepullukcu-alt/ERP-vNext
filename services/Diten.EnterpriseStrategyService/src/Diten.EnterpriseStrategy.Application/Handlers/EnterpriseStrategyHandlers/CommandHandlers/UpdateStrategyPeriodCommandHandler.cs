using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdateStrategyPeriodCommandHandler : IRequestHandler<UpdateStrategyPeriodCommand, Response<StrategyPeriodDto>>
{
    private readonly IPlanningCycleService _service;

    public UpdateStrategyPeriodCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodDto>> Handle(UpdateStrategyPeriodCommand request, CancellationToken cancellationToken) =>
        _service.UpdateStrategyPeriodAsync(request.StrategyPeriodId, request.StrategyPeriod, request.Actor, cancellationToken);
}
