using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class ArchiveGoalCommand : IRequest<Response<GoalDto>>
{
    public string GoalId { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public bool ArchiveGuardEnabled { get; set; } = true;
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
