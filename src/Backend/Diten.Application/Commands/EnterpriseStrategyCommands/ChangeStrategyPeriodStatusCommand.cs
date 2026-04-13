using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class ChangeStrategyPeriodStatusCommand : IRequest<Response<StrategyPeriodDto>>
{
    public string StrategyPeriodId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
