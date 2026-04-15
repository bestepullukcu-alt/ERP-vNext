using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Response<ProjectDetailDto>>
{
    private readonly IProjectOrchestrationService _service;

    public GetProjectByIdQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<ProjectDetailDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.ProjectId, cancellationToken);
}
