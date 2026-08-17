using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreateStrategyPeriodCommandHandler : IRequestHandler<CreateStrategyPeriodCommand, Response<StrategyPeriodDto>>
{
    private readonly IPlanningCycleService _service;

    public CreateStrategyPeriodCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodDto>> Handle(CreateStrategyPeriodCommand request, CancellationToken cancellationToken) =>
        _service.CreateStrategyPeriodAsync(request.StrategyPeriod, request.Actor, cancellationToken);
}
