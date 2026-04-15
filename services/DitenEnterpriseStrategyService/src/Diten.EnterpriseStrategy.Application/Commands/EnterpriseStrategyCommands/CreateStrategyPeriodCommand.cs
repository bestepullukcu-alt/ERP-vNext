using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class CreateStrategyPeriodCommand : IRequest<Response<StrategyPeriodDto>>
{
    public StrategyPeriodDto StrategyPeriod { get; set; } = new();
    public string Actor { get; set; } = "anonymous";
    public string CorrelationId { get; set; } = string.Empty;
}
