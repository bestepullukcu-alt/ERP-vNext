using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetConnectionCoverageGapsQueryHandler : IRequestHandler<GetConnectionCoverageGapsQuery, Response<IReadOnlyList<CoverageGapDto>>>
{
    private readonly IConnectionService _service;

    public GetConnectionCoverageGapsQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<IReadOnlyList<CoverageGapDto>>> Handle(GetConnectionCoverageGapsQuery request, CancellationToken cancellationToken) =>
        _service.CoverageGapsAsync(cancellationToken);
}
