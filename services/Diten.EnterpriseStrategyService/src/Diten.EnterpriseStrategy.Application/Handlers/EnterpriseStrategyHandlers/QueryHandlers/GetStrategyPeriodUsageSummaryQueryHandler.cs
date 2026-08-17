using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetStrategyPeriodUsageSummaryQueryHandler : IRequestHandler<GetStrategyPeriodUsageSummaryQuery, Response<StrategyPeriodUsageSummaryDto>>
{
    private readonly IPlanningCycleService _service;

    public GetStrategyPeriodUsageSummaryQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodUsageSummaryDto>> Handle(
        GetStrategyPeriodUsageSummaryQuery request,
        CancellationToken cancellationToken) =>
        _service.GetStrategyPeriodUsageSummaryAsync(request.StrategyPeriodId, cancellationToken);
}
