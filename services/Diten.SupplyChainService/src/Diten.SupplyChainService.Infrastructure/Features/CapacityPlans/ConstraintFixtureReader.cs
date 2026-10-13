using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
public sealed class ConstraintFixtureReader : IConstraintFixtureReader
{
    public bool IsExact(CapacityScope scope, CapacityScenario scenario) =>
        scope.TenantId == DemandFixtureReader.Tenant && scope.LegalEntityId == DemandFixtureReader.LegalEntity &&
        scenario.ConstraintRefs.Count == 1 && scenario.ConstraintRefs[0] == new CapacityConstraintReference("line-4-hours","SUPPLY-CONSTRAINTS","8") &&
        scenario.Adjustments.Count == 1 && scenario.Adjustments[0] == new CapacityAdjustment("line-4","2027-W03","80.000","HOUR");
    public bool CanEvaluate(CapacityScope scope, CapacityScenario scenario, CapacityEvaluation evaluation) =>
        IsExact(scope,scenario) && evaluation.ResourceRefs.Count == 1 && evaluation.ResourceRefs[0] == "line-4" &&
        evaluation.EvaluationMode is "Finite" or "Infinite";
}
