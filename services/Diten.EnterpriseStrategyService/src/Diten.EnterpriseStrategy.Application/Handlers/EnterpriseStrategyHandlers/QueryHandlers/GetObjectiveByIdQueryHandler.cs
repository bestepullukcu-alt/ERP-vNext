using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetObjectiveByIdQueryHandler : IRequestHandler<GetObjectiveByIdQuery, Response<ObjectiveDetailDto>>
{
    private readonly IObjectiveService _service;

    public GetObjectiveByIdQueryHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveDetailDto>> Handle(GetObjectiveByIdQuery request, CancellationToken cancellationToken) =>
        _service.GetAsync(request.ObjectiveId, cancellationToken);
}
