using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListStrategyPeriodsQueryHandler : IRequestHandler<ListStrategyPeriodsQuery, Response<IReadOnlyList<StrategyPeriodDto>>>
{
    private readonly IPlanningCycleService _service;

    public ListStrategyPeriodsQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<IReadOnlyList<StrategyPeriodDto>>> Handle(ListStrategyPeriodsQuery request, CancellationToken cancellationToken) =>
        _service.ListStrategyPeriodsAsync(request.PlanningCycleId, request.Search, request.Status, cancellationToken);
}
