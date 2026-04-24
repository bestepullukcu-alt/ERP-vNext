using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetStrategyPeriodByIdQueryHandler : IRequestHandler<GetStrategyPeriodByIdQuery, Response<StrategyPeriodDto>>
{
    private readonly IPlanningCycleService _service;

    public GetStrategyPeriodByIdQueryHandler(IPlanningCycleService service) => _service = service;

    public Task<Response<StrategyPeriodDto>> Handle(GetStrategyPeriodByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetStrategyPeriodAsync(request.StrategyPeriodId, cancellationToken);
}
