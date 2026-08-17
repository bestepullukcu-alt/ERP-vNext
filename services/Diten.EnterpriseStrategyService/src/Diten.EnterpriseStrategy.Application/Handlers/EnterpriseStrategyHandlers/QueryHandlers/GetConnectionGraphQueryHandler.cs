using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetConnectionGraphQueryHandler : IRequestHandler<GetConnectionGraphQuery, Response<ConnectionGraphViewDto>>
{
    private readonly IConnectionService _service;

    public GetConnectionGraphQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<ConnectionGraphViewDto>> Handle(GetConnectionGraphQuery request, CancellationToken cancellationToken) =>
        _service.GraphAsync(cancellationToken);
}
