using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetGoalSummaryQuery : IRequest<Response<GoalSummaryDto>>
{
    public string GoalId { get; set; } = string.Empty;
}
