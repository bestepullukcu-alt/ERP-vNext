using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed class EvaluateForecastHandler(ForecastWorkflow workflow)
    : IRequestHandler<EvaluateForecastQuery, Response<ForecastEvaluationView>>
{
    public Task<Response<ForecastEvaluationView>> Handle(EvaluateForecastQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return workflow.EvaluateAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasCreatePermission,
            request.PlanningCycleId, request.SkuId, request.WarehouseId,
            cancellationToken);
    }
}