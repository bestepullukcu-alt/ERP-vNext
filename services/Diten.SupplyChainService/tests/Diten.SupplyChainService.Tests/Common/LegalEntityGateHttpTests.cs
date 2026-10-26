using Diten.SupplyChainService.Application.Common;
using Xunit;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// H2, Returns and Claims: a create with an AuthService-shaped token is refused by the legal-entity gate when MDM
/// says the entity is not referenceable (Returns 404, Claims 403 — each keeps its convention, see
/// LegalEntityMiddlewareWiringTests) or cannot be reached (503), and nothing is written.
/// </summary>
[Collection("Returns Claims Http")]
public sealed class ReturnsClaimsLegalEntityGateHttpTests(ReturnsClaimsHttpHost host)
{
    [Theory]
    [InlineData("/api/shipment-bundle/returns", LegalEntityScopeOutcome.NotReferenceable, 404)]
    [InlineData("/api/shipment-bundle/returns", LegalEntityScopeOutcome.Unavailable, 503)]
    [InlineData("/api/shipment-bundle/claims", LegalEntityScopeOutcome.NotReferenceable, 403)]
    [InlineData("/api/shipment-bundle/claims", LegalEntityScopeOutcome.Unavailable, 503)]
    public async Task A_create_the_gate_refuses_writes_nothing(string path, LegalEntityScopeOutcome outcome, int expected)
    {
        var body = path.EndsWith("/returns", StringComparison.Ordinal) ? host.ReturnBody() : host.ClaimBody();
        await LegalEntityGate.AssertRefusedWithoutWrites(host.LegalEntityValidator, host.Database, host.Tenant, outcome, expected,
            async () => (await host.Send("POST", path, body, host.Token(), "gate-" + Guid.NewGuid())).Status);
    }
}

/// <summary>H2, S&amp;OP and Capacity: the same refusal over the shared bounded-slice host (404 / 503, no write).</summary>
[Collection(SandopCapacityHttpCollection.Name)]
public sealed class SandopCapacityLegalEntityGateHttpTests(SandopCapacityHttpHost host)
{
    [Theory]
    [InlineData("/api/supply-chain/sandop-plans", LegalEntityScopeOutcome.NotReferenceable, 404)]
    [InlineData("/api/supply-chain/sandop-plans", LegalEntityScopeOutcome.Unavailable, 503)]
    [InlineData("/api/supply-chain/capacity-plans", LegalEntityScopeOutcome.NotReferenceable, 404)]
    [InlineData("/api/supply-chain/capacity-plans", LegalEntityScopeOutcome.Unavailable, 503)]
    public async Task A_create_the_gate_refuses_writes_nothing(string path, LegalEntityScopeOutcome outcome, int expected)
    {
        var body = path.EndsWith("/sandop-plans", StringComparison.Ordinal)
            ? SandopCapacityHttpHost.SandopPlan()
            : SandopCapacityHttpHost.CapacityPlan();
        await LegalEntityGate.AssertRefusedWithoutWrites(host.LegalEntityValidator, host.Database, SandopCapacityHttpHost.Tenant,
            outcome, expected,
            async () => (await host.Send("POST", path, body, host.Token(), "gate-" + Guid.NewGuid())).Status);
    }
}
