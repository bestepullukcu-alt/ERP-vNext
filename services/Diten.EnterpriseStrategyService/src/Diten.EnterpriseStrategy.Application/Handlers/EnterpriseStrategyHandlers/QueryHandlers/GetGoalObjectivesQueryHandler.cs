using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetGoalObjectivesQueryHandler : IRequestHandler<GetGoalObjectivesQuery, Response<IReadOnlyList<ObjectiveDto>>>
{
    private readonly IGoalService _service;

    public GetGoalObjectivesQueryHandler(IGoalService service) => _service = service;

    public Task<Response<IReadOnlyList<ObjectiveDto>>> Handle(GetGoalObjectivesQuery request, CancellationToken cancellationToken) =>
        _service.GetObjectivesAsync(request.GoalId, cancellationToken);
}
