using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class ChangePlanningCycleStatusCommandHandler : IRequestHandler<ChangePlanningCycleStatusCommand, Response<PlanningCycleDto>>
{
    private readonly IPlanningCycleService _service;

    public ChangePlanningCycleStatusCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<PlanningCycleDto>> Handle(ChangePlanningCycleStatusCommand request, CancellationToken cancellationToken) =>
        _service.ChangePlanningCycleStatusAsync(request.PlanningCycleId, request.Status, request.Actor, cancellationToken);
}
