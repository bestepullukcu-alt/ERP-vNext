using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class CreateManualDraftHandler(ManualDraftWorkflow workflow)
    : IRequestHandler<CreateManualDraftCommand, Response<ManualDraftView>>
{
    public Task<Response<ManualDraftView>> Handle(CreateManualDraftCommand request,
        CancellationToken cancellationToken) =>
        workflow.CreateAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasPermission,
            request.PlanningCycleId, request.Reason, request.Series,
            request.IdempotencyKey, cancellationToken);
}
