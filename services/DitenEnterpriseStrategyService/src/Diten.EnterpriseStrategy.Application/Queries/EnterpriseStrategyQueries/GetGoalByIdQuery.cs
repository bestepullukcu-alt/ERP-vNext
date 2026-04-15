using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetGoalByIdQuery : IRequest<Response<GoalDetailDto>>
{
    public string GoalId { get; set; } = string.Empty;
}
