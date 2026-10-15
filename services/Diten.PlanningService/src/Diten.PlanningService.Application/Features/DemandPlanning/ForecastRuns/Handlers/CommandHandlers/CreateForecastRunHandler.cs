using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed class CreateForecastRunHandler(ForecastWorkflow workflow)
    : IRequestHandler<CreateForecastRunCommand, Response<ForecastCreateView>>
{
    public Task<Response<ForecastCreateView>> Handle(CreateForecastRunCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return workflow.CreateAsync(request.TenantId, request.ActorId,
            request.SelectedLegalEntityHint, request.HasRunPermission,
            request.HasDraftPermission, request.PlanningCycleId, request.SkuId,
            request.WarehouseId, request.Method, request.Reason,
            request.ExpectedCycleVersion, request.IdempotencyKey,
            cancellationToken);
    }
}