using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetInitiativeByIdQueryHandler : IRequestHandler<GetInitiativeByIdQuery, Response<InitiativeDetailDto>>
{
    private readonly IInitiativeOrchestrationService _service;

    public GetInitiativeByIdQueryHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<InitiativeDetailDto>> Handle(GetInitiativeByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.InitiativeId, cancellationToken);
}
