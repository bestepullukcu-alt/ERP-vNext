using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ResolveDefaultStrategyPeriodQueryHandler : IRequestHandler<ResolveDefaultStrategyPeriodQuery, Response<StrategyPeriodDto>>
{
    private readonly IPlanningCycleService _service;

    public ResolveDefaultStrategyPeriodQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodDto>> Handle(ResolveDefaultStrategyPeriodQuery request, CancellationToken cancellationToken) =>
        _service.ResolveDefaultForScopeAsync(request.CompanyId, request.BusinessUnitId, request.RegionId, cancellationToken);
}
