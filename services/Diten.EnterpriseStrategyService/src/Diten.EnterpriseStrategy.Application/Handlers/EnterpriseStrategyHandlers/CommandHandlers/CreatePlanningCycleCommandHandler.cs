using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.CommandHandlers;

public sealed class CreatePlanningCycleCommandHandler : IRequestHandler<CreatePlanningCycleCommand, Response<PlanningCycleDto>>
{
    private readonly IPlanningCycleService _service;

    public CreatePlanningCycleCommandHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<PlanningCycleDto>> Handle(CreatePlanningCycleCommand request, CancellationToken cancellationToken) =>
        _service.CreatePlanningCycleAsync(request.PlanningCycle, request.Actor, cancellationToken);
}
