using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed record TransitionManualDraftCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasPermission, Guid RevisionId,
    DraftReviewAction Action, string? Reason, string IdempotencyKey,
    int ExpectedContentVersion, int ExpectedStateVersion)
    : IRequest<Response<ManualDraftView>>;
