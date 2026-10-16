using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed record EvaluateForecastQuery(Guid TenantId, Guid ActorId,
    Guid SelectedLegalEntityHint, bool HasCreatePermission,
    Guid PlanningCycleId, Guid SkuId, string WarehouseId)
    : IRequest<Response<ForecastEvaluationView>>;