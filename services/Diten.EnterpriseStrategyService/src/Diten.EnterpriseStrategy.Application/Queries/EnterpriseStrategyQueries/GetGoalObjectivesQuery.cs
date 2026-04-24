using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetGoalObjectivesQuery : IRequest<Response<IReadOnlyList<ObjectiveDto>>>
{
    public string GoalId { get; set; } = string.Empty;
}
