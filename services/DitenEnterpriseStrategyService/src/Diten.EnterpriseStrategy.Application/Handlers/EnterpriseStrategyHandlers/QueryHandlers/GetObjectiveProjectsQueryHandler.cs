using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetObjectiveProjectsQueryHandler : IRequestHandler<GetObjectiveProjectsQuery, Response<IReadOnlyList<ProjectStrategyLinkViewDto>>>
{
    private readonly IObjectiveService _service;

    public GetObjectiveProjectsQueryHandler(IObjectiveService service) => _service = service;

    public Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> Handle(
        GetObjectiveProjectsQuery request,
        CancellationToken cancellationToken) =>
        _service.GetProjectsAsync(request.ObjectiveId, cancellationToken);
}
