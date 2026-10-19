using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed record CreateManualDraftCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasPermission, Guid PlanningCycleId,
    string Reason, IReadOnlyList<ManualDraftSeriesInput> Series,
    string IdempotencyKey) : IRequest<Response<ManualDraftView>>;
