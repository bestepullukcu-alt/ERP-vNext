using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListGoalsQuery : IRequest<Response<PagedResponseDto<GoalDto>>>
{
    public PagedRequestDto Request { get; set; } = new();
}
