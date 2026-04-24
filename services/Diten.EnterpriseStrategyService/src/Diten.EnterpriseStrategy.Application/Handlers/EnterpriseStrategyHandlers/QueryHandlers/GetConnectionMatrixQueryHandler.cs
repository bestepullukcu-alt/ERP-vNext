using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetConnectionMatrixQueryHandler : IRequestHandler<GetConnectionMatrixQuery, Response<IReadOnlyList<ConnectionMatrixCellDto>>>
{
    private readonly IConnectionService _service;

    public GetConnectionMatrixQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<IReadOnlyList<ConnectionMatrixCellDto>>> Handle(GetConnectionMatrixQuery request, CancellationToken cancellationToken) =>
        _service.MatrixAsync(request.Mode, cancellationToken);
}
