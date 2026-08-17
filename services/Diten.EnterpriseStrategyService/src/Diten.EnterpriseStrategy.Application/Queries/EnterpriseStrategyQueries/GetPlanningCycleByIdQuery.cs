using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetPlanningCycleByIdQuery : IRequest<Response<PlanningCycleDto>>
{
    public string PlanningCycleId { get; set; } = string.Empty;
}
