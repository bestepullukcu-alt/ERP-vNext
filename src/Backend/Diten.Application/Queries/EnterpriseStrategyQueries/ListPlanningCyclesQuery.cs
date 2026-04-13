using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListPlanningCyclesQuery : IRequest<Response<IReadOnlyList<PlanningCycleDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
}
