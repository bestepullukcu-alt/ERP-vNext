using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed record GetManualDraftQuery(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasPermission, Guid RevisionId)
    : IRequest<Response<ManualDraftView>>;
