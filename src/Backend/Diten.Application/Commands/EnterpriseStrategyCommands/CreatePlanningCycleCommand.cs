using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class CreatePlanningCycleCommand : IRequest<Response<PlanningCycleDto>>
{
    public PlanningCycleDto PlanningCycle { get; set; } = new();
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
