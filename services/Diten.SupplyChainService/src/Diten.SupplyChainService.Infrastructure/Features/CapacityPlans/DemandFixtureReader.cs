using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
public sealed class DemandFixtureReader : IDemandFixtureReader
{
    // The bounded fixture is an authority only in its one explicit scope.
    public static readonly Guid Tenant = Guid.Parse("19200000-0000-4000-8000-000000000001");
    public static readonly Guid LegalEntity = Guid.Parse("19200000-0000-4000-8000-000000000002");
    public bool IsExact(CapacityScope scope, CapacityPlan plan) =>
        scope.TenantId == Tenant && scope.LegalEntityId == LegalEntity &&
        plan.DemandPlanId == "dp-2027" && plan.DemandPlanVersion == "3" &&
        plan.SourceChecksum == "sha256:ee56d4f9a3c8";
}
