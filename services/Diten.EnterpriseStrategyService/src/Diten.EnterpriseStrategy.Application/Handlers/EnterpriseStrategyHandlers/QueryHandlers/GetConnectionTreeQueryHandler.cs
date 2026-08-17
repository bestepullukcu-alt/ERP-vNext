using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetConnectionTreeQueryHandler : IRequestHandler<GetConnectionTreeQuery, Response<IReadOnlyList<ConnectionTreeNodeDto>>>
{
    private readonly IConnectionService _service;

    public GetConnectionTreeQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<IReadOnlyList<ConnectionTreeNodeDto>>> Handle(GetConnectionTreeQuery request, CancellationToken cancellationToken) =>
        _service.TreeAsync(cancellationToken);
}
