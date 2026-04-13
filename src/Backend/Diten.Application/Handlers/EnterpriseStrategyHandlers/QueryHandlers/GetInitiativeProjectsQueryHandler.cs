using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetInitiativeProjectsQueryHandler : IRequestHandler<GetInitiativeProjectsQuery, Response<IReadOnlyList<ProjectStrategyLinkViewDto>>>
{
    private readonly IInitiativeOrchestrationService _service;

    public GetInitiativeProjectsQueryHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> Handle(
        GetInitiativeProjectsQuery request,
        CancellationToken cancellationToken) =>
        _service.ProjectsAsync(request.InitiativeId, cancellationToken);
}
