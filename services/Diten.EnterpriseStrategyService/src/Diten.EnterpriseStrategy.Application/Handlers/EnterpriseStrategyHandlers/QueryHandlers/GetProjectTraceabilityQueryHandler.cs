using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetProjectTraceabilityQueryHandler : IRequestHandler<GetProjectTraceabilityQuery, Response<string>>
{
    private readonly IProjectOrchestrationService _service;

    public GetProjectTraceabilityQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<string>> Handle(GetProjectTraceabilityQuery request, CancellationToken cancellationToken) =>
        _service.TraceabilityAsync(request.ProjectId, cancellationToken);
}
