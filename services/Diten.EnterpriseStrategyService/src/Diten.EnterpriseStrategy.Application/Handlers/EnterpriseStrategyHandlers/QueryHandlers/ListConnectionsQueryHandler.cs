using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListConnectionsQueryHandler
    : IRequestHandler<ListConnectionsQuery, Response<PagedResponseDto<StrategyConnectionDto>>>
{
    private readonly IConnectionService _service;

    public ListConnectionsQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<PagedResponseDto<StrategyConnectionDto>>> Handle(ListConnectionsQuery request, CancellationToken cancellationToken) =>
        _service.ListAsync(request.Request, cancellationToken);
}
