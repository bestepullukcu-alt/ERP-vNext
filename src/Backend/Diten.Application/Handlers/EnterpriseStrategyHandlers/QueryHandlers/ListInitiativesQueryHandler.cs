using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListInitiativesQueryHandler : IRequestHandler<ListInitiativesQuery, Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>>
{
    private readonly IInitiativeOrchestrationService _service;

    public ListInitiativesQueryHandler(IInitiativeOrchestrationService service) => _service = service;

    public Task<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>> Handle(ListInitiativesQuery request, CancellationToken cancellationToken) =>
        _service.ListAsync(request.Request, cancellationToken);
}
