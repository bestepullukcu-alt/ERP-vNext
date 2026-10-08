using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed record ExcludeManualDraftSeriesCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasPermission, Guid RevisionId,
    Guid SkuId, string WarehouseId, string Reason, string IdempotencyKey,
    int ExpectedContentVersion, int ExpectedStateVersion)
    : IRequest<Response<ManualDraftView>>;
