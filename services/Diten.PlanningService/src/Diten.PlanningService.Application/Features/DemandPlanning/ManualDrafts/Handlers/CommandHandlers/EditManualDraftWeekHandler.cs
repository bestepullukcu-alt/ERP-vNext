using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class EditManualDraftWeekHandler(ManualDraftWorkflow workflow)
    : IRequestHandler<EditManualDraftWeekCommand, Response<ManualDraftView>>
{
    public Task<Response<ManualDraftView>> Handle(EditManualDraftWeekCommand request,
        CancellationToken cancellationToken) =>
        workflow.EditWeekAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasPermission,
            request.RevisionId, request.SkuId, request.WarehouseId,
            request.WeekNumber, request.ValueKind, request.Quantity,
            request.Reason, request.IdempotencyKey,
            request.ExpectedContentVersion, cancellationToken);
}
