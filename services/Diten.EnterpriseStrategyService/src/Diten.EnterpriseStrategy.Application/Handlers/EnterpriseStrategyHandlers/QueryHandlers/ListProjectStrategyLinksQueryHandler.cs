using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListProjectStrategyLinksQueryHandler
    : IRequestHandler<ListProjectStrategyLinksQuery, Response<PagedResponseDto<ProjectStrategyLinkViewDto>>>
{
    private readonly IObjectiveService _service;

    public ListProjectStrategyLinksQueryHandler(IObjectiveService service)
    {
        _service = service;
    }

    public async Task<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>> Handle(
        ListProjectStrategyLinksQuery request,
        CancellationToken cancellationToken)
    {
        var list = await _service.GetProjectsAsync(
            request.Request.Filters.TryGetValue("objectiveId", out var objectiveId) ? objectiveId : string.Empty,
            cancellationToken);
        var items = list.Data ?? Array.Empty<ProjectStrategyLinkViewDto>();

        return Response<PagedResponseDto<ProjectStrategyLinkViewDto>>.Ok(new PagedResponseDto<ProjectStrategyLinkViewDto>
        {
            Page = request.Request.Page,
            PageSize = request.Request.PageSize,
            TotalCount = items.Count,
            Items = items
        });
    }
}
