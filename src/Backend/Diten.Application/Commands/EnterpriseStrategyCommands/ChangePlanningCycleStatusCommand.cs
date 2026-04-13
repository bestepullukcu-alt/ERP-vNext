using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class ChangePlanningCycleStatusCommand : IRequest<Response<PlanningCycleDto>>
{
    public string PlanningCycleId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
