namespace Diten.SupplyChainService.Domain.Features.CapacityPlans;
public sealed record CapacityScope(Guid TenantId, Guid LegalEntityId, Guid ActorId)
{
    public void EnsureTrusted()
    {
        if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ActorId == Guid.Empty)
            throw new InvalidOperationException("Trusted Capacity scope is required.");
    }
}
public interface IDemandFixtureReader
{
    bool IsExact(CapacityScope scope, CapacityPlan plan);
}
public interface IConstraintFixtureReader
{
    bool IsExact(CapacityScope scope, CapacityScenario scenario);
    bool CanEvaluate(CapacityScope scope, CapacityScenario scenario, CapacityEvaluation evaluation);
}
