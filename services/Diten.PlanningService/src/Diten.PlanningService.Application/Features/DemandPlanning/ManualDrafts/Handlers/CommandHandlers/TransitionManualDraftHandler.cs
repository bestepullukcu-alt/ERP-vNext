using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class TransitionManualDraftHandler(ManualDraftWorkflow workflow)
    : IRequestHandler<TransitionManualDraftCommand, Response<ManualDraftView>>
{
    public Task<Response<ManualDraftView>> Handle(TransitionManualDraftCommand request,
        CancellationToken cancellationToken) =>
        workflow.TransitionAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasPermission,
            request.RevisionId, request.Action, request.Reason,
            request.IdempotencyKey, request.ExpectedContentVersion,
            request.ExpectedStateVersion, cancellationToken);
}
