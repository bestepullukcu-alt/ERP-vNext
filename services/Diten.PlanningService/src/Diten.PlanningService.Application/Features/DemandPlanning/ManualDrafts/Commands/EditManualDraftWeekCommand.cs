using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

public sealed record EditManualDraftWeekCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasPermission, Guid RevisionId,
    Guid SkuId, string WarehouseId, int WeekNumber, DraftWeekValueKind ValueKind,
    decimal? Quantity, string Reason, string IdempotencyKey,
    int ExpectedContentVersion) : IRequest<Response<ManualDraftView>>;
