using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed record CreateForecastRunCommand(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasRunPermission, bool HasDraftPermission,
    Guid PlanningCycleId, Guid SkuId, string WarehouseId,
    string Method, string Reason, int ExpectedCycleVersion, string IdempotencyKey)
    : IRequest<Response<ForecastCreateView>>;