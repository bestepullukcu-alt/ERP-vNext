using System.Text.Json.Nodes;
using Xunit;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// What the bounded-slice HTTP host can and cannot reach, asserted rather than assumed.
/// Measured 2026-10-11: registering the fixture readers and the two excluded feature namespaces' handlers is
/// enough for Program.cs's ComposedFeatureControllerFilter (Program.cs:115/:151) to keep BOTH controllers — so
/// MOD-0190 is reachable over HTTP. MOD-0192 is not, and not for the reason the ledger records.
/// </summary>
[Collection(SandopCapacityHttpCollection.Name)]
public sealed class SandopCapacityHostCompositionTests
{
    private readonly SandopCapacityHttpHost _host;
    public SandopCapacityHostCompositionTests(SandopCapacityHttpHost host) => _host = host;

    [Fact]
    public async Task Sandop_controller_is_mapped_and_serves_a_request()
    {
        var reply = await _host.Send("POST", "/api/supply-chain/sandop-plans",
            SandopCapacityHttpHost.SandopPlan(), _host.Token(), "k-" + Guid.NewGuid());
        Assert.True(reply.Status == 201, $"expected 201, got {reply.Status}: {reply.Body}");
    }

    /// <summary>
    /// A known, deliberate gap that must STILL be a gap, so the entry cannot outlive its reason
    /// (the pattern CompositionRootGuardTests.KnownUncomposed uses).
    ///
    /// MOD-0192's CapacityContextMiddleware is never added to the pipeline. Program.cs wires five siblings —
    /// Carrier (:132), Load (:133), Return (:141), Claim (:142) and Shipment (:145) — and the whole of src
    /// contains exactly one reference to CapacityContextMiddleware: its own declaration. So CapacityRequestContext
    /// is never populated and every authenticated Capacity request answers 500, even with the module fully
    /// composed as it is here. This is NOT the §22 uptake gate (Q292/Q273, the missing demand producer): it would
    /// still be true the day a real IDemandFixtureReader arrives.
    ///
    /// Fixing it means editing Program.cs, which the §22 gate reserves, so this test records the defect instead.
    /// When the middleware is wired, this test fails and must be replaced by the real Capacity HTTP assertions —
    /// MOD-0192's acceptance :199, :200 and :201 are open for exactly this reason (:202 closed 2026-10-12 by
    /// SandopCapacitySeparationTests, which needs no HTTP).
    ///
    /// 2026-10-12: CT wired the branch, watched this guard fire exactly as designed, measured the result, and
    /// then reverted it — Program.cs is hash-pinned as Accepted composition in both packs (MOD-0192:273,
    /// MOD-0190:261) and CT did not have the integration-owner authorization. The measurement survives in
    /// MOD-0192:199: the correlation chain is green end to end and all five pipeline suites were byte-identical,
    /// so whoever holds that authorization has the evidence and the two lines ready. CapacityCorrelationTests
    /// is the test to un-skip on that day.
    /// </summary>
    [Fact]
    public async Task Capacity_controller_is_mapped_but_its_context_middleware_is_not_composed()
    {
        var reply = await _host.Send("POST", "/api/supply-chain/capacity-plans",
            SandopCapacityHttpHost.CapacityPlan(), _host.Token(), "k-" + Guid.NewGuid());

        Assert.True(reply.Status == 500,
            "Capacity answered " + reply.Status + ", not 500. If the Capacity context middleware has been wired "
            + "into Program.cs, this known-gap guard is stale: delete it and close MOD-0192 :199/:200/:201/:202 "
            + "with real HTTP assertions. Body: " + reply.Body);

        // 500 alone could be anything; pin it to the contract envelope so an unrelated failure does not pass here.
        Assert.Equal("INTERNAL_ERROR", reply.Code);
    }
}
