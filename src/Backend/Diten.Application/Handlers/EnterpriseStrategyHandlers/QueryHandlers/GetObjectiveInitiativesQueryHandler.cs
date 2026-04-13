using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetObjectiveInitiativesQueryHandler : IRequestHandler<GetObjectiveInitiativesQuery, Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>>
{
    private readonly IObjectiveService _service;

    public GetObjectiveInitiativesQueryHandler(IObjectiveService service) => _service = service;

    public Task<Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>> Handle(
        GetObjectiveInitiativesQuery request,
        CancellationToken cancellationToken) =>
        _service.GetInitiativesAsync(request.ObjectiveId, cancellationToken);
}
