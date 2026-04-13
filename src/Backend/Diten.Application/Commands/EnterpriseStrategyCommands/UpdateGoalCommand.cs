using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class UpdateGoalCommand : IRequest<Response<GoalDto>>
{
    public string GoalId { get; set; } = string.Empty;
    public GoalDto Goal { get; set; } = new();
    public int ExpectedVersion { get; set; }
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
