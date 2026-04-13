using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetGoalSummaryQueryHandler : IRequestHandler<GetGoalSummaryQuery, Response<GoalSummaryDto>>
{
    private readonly IGoalService _service;

    public GetGoalSummaryQueryHandler(IGoalService service) => _service = service;

    public Task<Response<GoalSummaryDto>> Handle(GetGoalSummaryQuery request, CancellationToken cancellationToken) =>
        _service.GetSummaryAsync(request.GoalId, cancellationToken);
}
