using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetGoalByIdQueryHandler : IRequestHandler<GetGoalByIdQuery, Response<GoalDetailDto>>
{
    private readonly IGoalService _service;

    public GetGoalByIdQueryHandler(IGoalService service) => _service = service;

    public Task<Response<GoalDetailDto>> Handle(GetGoalByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.GoalId, cancellationToken);
}
