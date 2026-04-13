using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetProjectUpstreamLineageQueryHandler : IRequestHandler<GetProjectUpstreamLineageQuery, Response<string>>
{
    private readonly IProjectOrchestrationService _service;

    public GetProjectUpstreamLineageQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<string>> Handle(GetProjectUpstreamLineageQuery request, CancellationToken cancellationToken) =>
        _service.UpstreamLineageAsync(request.ProjectId, cancellationToken);
}
