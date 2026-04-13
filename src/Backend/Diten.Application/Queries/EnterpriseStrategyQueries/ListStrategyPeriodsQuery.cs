using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListStrategyPeriodsQuery : IRequest<Response<IReadOnlyList<StrategyPeriodDto>>>
{
    public string? PlanningCycleId { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
}
