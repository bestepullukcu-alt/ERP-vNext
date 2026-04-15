using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class UpdatePlanningCycleCommandHandler : IRequestHandler<UpdatePlanningCycleCommand, Response<PlanningCycleDto>>
{
    private readonly IPlanningCycleService _service;

    public UpdatePlanningCycleCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<PlanningCycleDto>> Handle(UpdatePlanningCycleCommand request, CancellationToken cancellationToken) =>
        _service.UpdatePlanningCycleAsync(request.PlanningCycleId, request.PlanningCycle, request.Actor, cancellationToken);
}
