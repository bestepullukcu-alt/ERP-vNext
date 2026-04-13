using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetEnterpriseStrategyOverviewQueryHandler
    : IRequestHandler<GetEnterpriseStrategyOverviewQuery, Response<EnterpriseStrategyOverviewDto>>
{
    private readonly IGoalService _goals;
    private readonly IObjectiveService _objectives;
    private readonly IConnectionService _connections;

    public GetEnterpriseStrategyOverviewQueryHandler(
        IGoalService goals,
        IObjectiveService objectives,
        IConnectionService connections)
    {
        _goals = goals;
        _objectives = objectives;
        _connections = connections;
    }

    public async Task<Response<EnterpriseStrategyOverviewDto>> Handle(
        GetEnterpriseStrategyOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var goals = await _goals.ListAsync(new PagedRequestDto { Page = 1, PageSize = 200 }, cancellationToken);
        var objectives = await _objectives.ListAsync(new PagedRequestDto { Page = 1, PageSize = 500 }, cancellationToken);
        var gaps = await _connections.CoverageGapsAsync(cancellationToken);

        var dto = new EnterpriseStrategyOverviewDto
        {
            GoalsCount = goals.Data?.TotalCount ?? 0,
            ObjectivesCount = objectives.Data?.TotalCount ?? 0,
            ActiveGoalsCount = goals.Data?.Items.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)) ?? 0,
            ActiveObjectivesCount = objectives.Data?.Items.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)) ?? 0,
            ConnectionGapsCount = gaps.Data?.Count ?? 0
        };

        return Response<EnterpriseStrategyOverviewDto>.Ok(dto);
    }
}
