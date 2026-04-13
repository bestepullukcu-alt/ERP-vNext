using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetPlanningCycleByIdQueryHandler : IRequestHandler<GetPlanningCycleByIdQuery, Response<PlanningCycleDto>>
{
    private readonly IPlanningCycleService _service;

    public GetPlanningCycleByIdQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<PlanningCycleDto>> Handle(GetPlanningCycleByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetPlanningCycleAsync(request.PlanningCycleId, cancellationToken);
}
