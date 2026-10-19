using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;

/// <summary>
/// The one demand plan this test-only seam accepts, in its one explicit scope. G4 (Wave 6.1, 2026-10-18): the scope
/// can be supplied by a non-production configuration seed, because a configured tenant/legal entity is the only way a
/// composed Development/Staging host can use it — MDM assigns legal-entity ids itself, so the bounded constants below
/// can never exist there. Without a configured fixture the bounded constants stay the authority (the module tests).
/// </summary>
public sealed record CapacityDemandFixture(Guid TenantId, Guid LegalEntityId, string DemandPlanId, string DemandPlanVersion, string SourceChecksum);

public sealed class DemandFixtureReader : IDemandFixtureReader
{
    // The bounded fixture is an authority only in its one explicit scope.
    public static readonly Guid Tenant = Guid.Parse("19200000-0000-4000-8000-000000000001");
    public static readonly Guid LegalEntity = Guid.Parse("19200000-0000-4000-8000-000000000002");
    public static readonly CapacityDemandFixture Bounded = new(Tenant, LegalEntity, "dp-2027", "3", "sha256:ee56d4f9a3c8");

    private readonly CapacityDemandFixture _fixture;

    public DemandFixtureReader() : this(Bounded) { }

    public DemandFixtureReader(CapacityDemandFixture fixture) => _fixture = fixture;

    public bool IsExact(CapacityScope scope, CapacityPlan plan) =>
        scope.TenantId == _fixture.TenantId && scope.LegalEntityId == _fixture.LegalEntityId &&
        plan.DemandPlanId == _fixture.DemandPlanId && plan.DemandPlanVersion == _fixture.DemandPlanVersion &&
        plan.SourceChecksum == _fixture.SourceChecksum;
}
