using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using MediatR;

namespace Diten.Application.Handlers.EnterpriseStrategyHandlers.QueryHandlers;

public sealed class GetObjectiveAlignmentSummaryQueryHandler : IRequestHandler<GetObjectiveAlignmentSummaryQuery, Response<ObjectiveAlignmentSummaryDto>>
{
    private readonly IObjectiveService _service;

    public GetObjectiveAlignmentSummaryQueryHandler(IObjectiveService service) => _service = service;

    public Task<Response<ObjectiveAlignmentSummaryDto>> Handle(
        GetObjectiveAlignmentSummaryQuery request,
        CancellationToken cancellationToken) =>
        _service.GetAlignmentSummaryAsync(request.ObjectiveId, cancellationToken);
}
