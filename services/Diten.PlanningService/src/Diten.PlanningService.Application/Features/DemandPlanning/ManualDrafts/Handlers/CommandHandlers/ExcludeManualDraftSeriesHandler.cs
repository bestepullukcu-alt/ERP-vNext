using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class ExcludeManualDraftSeriesHandler(ManualDraftWorkflow workflow)
    : IRequestHandler<ExcludeManualDraftSeriesCommand, Response<ManualDraftView>>
{
    public Task<Response<ManualDraftView>> Handle(ExcludeManualDraftSeriesCommand request,
        CancellationToken cancellationToken) =>
        workflow.ExcludeSeriesAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasPermission,
            request.RevisionId, request.SkuId, request.WarehouseId,
            request.Reason, request.IdempotencyKey, request.ExpectedContentVersion,
            request.ExpectedStateVersion, cancellationToken);
}
