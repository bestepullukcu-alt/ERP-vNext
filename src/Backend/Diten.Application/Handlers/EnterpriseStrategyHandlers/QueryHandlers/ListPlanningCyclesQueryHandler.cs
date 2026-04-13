using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListPlanningCyclesQueryHandler : IRequestHandler<ListPlanningCyclesQuery, Response<IReadOnlyList<PlanningCycleDto>>>
{
    private readonly IPlanningCycleService _service;

    public ListPlanningCyclesQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<IReadOnlyList<PlanningCycleDto>>> Handle(ListPlanningCyclesQuery request, CancellationToken cancellationToken) =>
        _service.ListPlanningCyclesAsync(request.Search, request.Status, cancellationToken);
}
