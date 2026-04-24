using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetInitiativeTraceabilityQueryHandler : IRequestHandler<GetInitiativeTraceabilityQuery, Response<string>>
{
    private readonly IInitiativeOrchestrationService _service;

    public GetInitiativeTraceabilityQueryHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<string>> Handle(GetInitiativeTraceabilityQuery request, CancellationToken cancellationToken) =>
        _service.TraceabilityAsync(request.InitiativeId, cancellationToken);
}
