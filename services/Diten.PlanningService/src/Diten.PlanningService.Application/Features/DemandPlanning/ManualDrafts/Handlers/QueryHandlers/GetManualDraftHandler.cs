using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed class GetManualDraftHandler(ManualDraftWorkflow workflow)
    : IRequestHandler<GetManualDraftQuery, Response<ManualDraftView>>
{
    public Task<Response<ManualDraftView>> Handle(GetManualDraftQuery request,
        CancellationToken cancellationToken) =>
        workflow.ReadAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasPermission,
            request.RevisionId, cancellationToken);
}
