using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListInitiativeStrategyLinksQueryHandler
    : IRequestHandler<ListInitiativeStrategyLinksQuery, Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>>
{
    private readonly IObjectiveService _service;

    public ListInitiativeStrategyLinksQueryHandler(IObjectiveService service)
    {
        _service = service;
    }

    public async Task<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>> Handle(
        ListInitiativeStrategyLinksQuery request,
        CancellationToken cancellationToken)
    {
        var list = await _service.GetInitiativesAsync(
            request.Request.Filters.TryGetValue("objectiveId", out var objectiveId) ? objectiveId : string.Empty,
            cancellationToken);
        var items = list.Data ?? Array.Empty<InitiativeStrategyLinkViewDto>();

        return Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>.Ok(new PagedResponseDto<InitiativeStrategyLinkViewDto>
        {
            Page = request.Request.Page,
            PageSize = request.Request.PageSize,
            TotalCount = items.Count,
            Items = items
        });
    }
}
