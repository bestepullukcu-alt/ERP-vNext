using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetCompatibleProjectTemplatesQueryHandler : IRequestHandler<GetCompatibleProjectTemplatesQuery, Response<IReadOnlyList<ProjectCreationTemplateDto>>>
{
    private readonly IProjectOrchestrationService _service;

    public GetCompatibleProjectTemplatesQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<IReadOnlyList<ProjectCreationTemplateDto>>> Handle(
        GetCompatibleProjectTemplatesQuery request,
        CancellationToken cancellationToken) =>
        _service.GetCompatibleTemplatesAsync(request.ParentType, request.EntityScope, cancellationToken);
}
