using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Application.Features.CapacityPlans;
public static class CapacityProjection
{
    public static CapacityPlanResponse Plan(CapacityPlan x) => new(x.Id,x.Name,x.HorizonStart,x.HorizonEnd,new(x.DemandPlanId,x.DemandPlanVersion,"DEMAND","v1",x.SourceCapturedAt,x.SourceChecksum),x.Status,x.CreatedAt);
    public static CapacityScenarioResponse Scenario(CapacityScenario x) => new(x.Id,x.CapacityPlanId,x.Name,x.ConstraintRefs,x.Adjustments,x.Status,x.CreatedAt);
    public static CapacityEvaluationResponse Evaluation(CapacityEvaluation x) => new(x.Id,x.CapacityPlanId,x.ScenarioId,x.EvaluationMode,x.Status,x.Bottlenecks,x.SubmittedAt,x.CompletedAt is { } at ? new DateTimeOffset(at,TimeSpan.Zero) : null);
}
