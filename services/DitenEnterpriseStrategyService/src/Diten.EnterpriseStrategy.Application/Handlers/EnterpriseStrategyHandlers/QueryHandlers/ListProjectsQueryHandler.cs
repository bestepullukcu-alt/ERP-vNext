using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, Response<PagedResponseDto<ProjectStrategyLinkViewDto>>>
{
    private readonly IProjectOrchestrationService _service;

    public ListProjectsQueryHandler(IProjectOrchestrationService service) => _service = service;

    public Task<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>> Handle(ListProjectsQuery request, CancellationToken cancellationToken) =>
        _service.ListAsync(request.Request, cancellationToken);
}
