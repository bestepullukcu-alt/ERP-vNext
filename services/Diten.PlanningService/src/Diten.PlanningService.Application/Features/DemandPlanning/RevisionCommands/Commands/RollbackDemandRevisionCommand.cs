using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;

public sealed record RollbackDemandRevisionCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, Guid SourceRevisionId, bool HasCreatePermission,
    string Reason, string IdempotencyKey, int ExpectedSourceStateVersion)
    : IRequest<Response<ManualDraftView>>;
