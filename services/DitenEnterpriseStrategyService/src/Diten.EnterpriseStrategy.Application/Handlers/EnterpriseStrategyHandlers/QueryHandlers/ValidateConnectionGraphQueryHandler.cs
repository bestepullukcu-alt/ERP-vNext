using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ValidateConnectionGraphQueryHandler : IRequestHandler<ValidateConnectionGraphQuery, Response<ConnectionGraphViewDto>>
{
    private readonly IConnectionService _service;

    public ValidateConnectionGraphQueryHandler(IConnectionService service) => _service = service;

    public Task<Response<ConnectionGraphViewDto>> Handle(ValidateConnectionGraphQuery request, CancellationToken cancellationToken) =>
        _service.ValidateGraphAsync(cancellationToken);
}
