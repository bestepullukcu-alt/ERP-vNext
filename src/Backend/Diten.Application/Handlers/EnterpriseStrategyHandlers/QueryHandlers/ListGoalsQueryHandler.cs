using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListGoalsQueryHandler
    : IRequestHandler<ListGoalsQuery, Response<PagedResponseDto<GoalDto>>>
{
    private readonly IGoalService _service;

    public ListGoalsQueryHandler(IGoalService service) => _service = service;

    public Task<Response<PagedResponseDto<GoalDto>>> Handle(ListGoalsQuery request, CancellationToken cancellationToken) =>
        _service.ListAsync(request.Request, cancellationToken);
}
