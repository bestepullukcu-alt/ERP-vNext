using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetProjectAuditTrailQueryHandler : IRequestHandler<GetProjectAuditTrailQuery, Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>>
{
    private readonly IProjectOrchestrationService _service;

    public GetProjectAuditTrailQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>> Handle(
        GetProjectAuditTrailQuery request,
        CancellationToken cancellationToken) =>
        _service.GetAuditTrailAsync(request.ProjectId, cancellationToken);
}
