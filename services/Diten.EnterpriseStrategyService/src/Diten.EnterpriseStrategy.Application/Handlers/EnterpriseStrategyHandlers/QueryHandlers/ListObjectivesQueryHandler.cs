using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class ListObjectivesQueryHandler
    : IRequestHandler<ListObjectivesQuery, Response<PagedResponseDto<ObjectiveDto>>>
{
    private readonly IObjectiveService _service;

    public ListObjectivesQueryHandler(IObjectiveService service) => _service = service;

    public Task<Response<PagedResponseDto<ObjectiveDto>>> Handle(ListObjectivesQuery request, CancellationToken cancellationToken) =>
        _service.ListAsync(request.Request, cancellationToken);
}
