using Xunit;
using Diten.SupplyChainService.Tests.Common;

namespace Diten.SupplyChainService.Tests.CapacityPlans;

/// <summary>
/// R-2 alignment (owner decision 2026-10-18). AuthService stopped issuing legal_entity_id at R-2 (66250b1a1), and the
/// Capacity gate kept requiring it — every real user got 403. Now the legal entity is X-Legal-Entity-Id alone, MDM
/// validates it, and the token carries tenant and actor only (the shared host mints exactly that). Measured over the
/// real pipeline.
/// </summary>
[Collection(SandopCapacityHttpCollection.Name)]
public sealed class CapacityLegalEntityScopeTests
{
    private const string Plans = "/api/supply-chain/capacity-plans";
    private readonly SandopCapacityHttpHost _host;
    public CapacityLegalEntityScopeTests(SandopCapacityHttpHost host) => _host = host;

    [Fact]
    public async Task A_token_without_legal_entity_id_creates_and_reads_in_the_header_scope()
    {
        // A plan is unique per horizon and demand version; an own horizon keeps this independent of the other suites.
        var year = 2300 + Random.Shared.Next(0, 6000);
        var plan = SandopCapacityHttpHost.CapacityPlan();
        plan["horizonStart"] = $"{year}-01-01";
        plan["horizonEnd"] = $"{year}-12-31";
        var created = await _host.Send("POST", Plans, plan, _host.Token(), "k-" + Guid.NewGuid());
        Assert.Equal(201, created.Status);
        var id = created.Json!["capacityPlanId"]!.GetValue<string>();
        Assert.Equal(200, (await _host.Send("GET", $"{Plans}/{id}", null, _host.Token(), null)).Status);
        // Another legal entity of the same tenant, same token: its scope holds nothing — the module's own 404.
        var foreign = await _host.Send("GET", $"{Plans}/{id}", null, _host.Token(), null, legalEntity: Guid.NewGuid());
        Assert.Equal(404, foreign.Status);
        Assert.Equal("UNKNOWN_CAPACITY_PLAN", foreign.Code);
    }

    [Fact]
    public async Task Without_X_Legal_Entity_Id_the_gate_answers_400()
    {
        var reply = await _host.Send("GET", $"{Plans}/{Guid.NewGuid()}", null, _host.Token(), null,
            alter: r => r.Headers.Remove("X-Legal-Entity-Id"));
        Assert.Equal(400, reply.Status);
        Assert.Equal("INVALID_REQUEST", reply.Code);
    }

    [Fact]
    public async Task A_tenant_header_that_disagrees_with_the_token_is_403()
    {
        var reply = await _host.Send("GET", $"{Plans}/{Guid.NewGuid()}", null, _host.Token(), null, tenant: Guid.NewGuid());
        Assert.Equal(403, reply.Status);
        Assert.Equal("FORBIDDEN", reply.Code);
    }
}
