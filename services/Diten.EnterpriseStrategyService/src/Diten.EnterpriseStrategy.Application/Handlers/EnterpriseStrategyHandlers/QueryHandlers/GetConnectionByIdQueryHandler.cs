using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetConnectionByIdQueryHandler : IRequestHandler<GetConnectionByIdQuery, Response<StrategyConnectionDto>>
{
    private readonly IConnectionService _service;

    public GetConnectionByIdQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<StrategyConnectionDto>> Handle(GetConnectionByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.ConnectionId, cancellationToken);
}
