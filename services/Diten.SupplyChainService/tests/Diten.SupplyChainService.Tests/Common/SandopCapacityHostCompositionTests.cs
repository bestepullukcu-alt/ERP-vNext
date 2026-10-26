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
    /// Q484 (2026-10-12): until the integration owner released it, this test recorded a defect instead of
    /// asserting the contract — Program.cs wired five context middlewares (Carrier, Load, Return, Claim,
    /// Shipment) and the whole of src held exactly one reference to CapacityContextMiddleware, its own
    /// declaration. So a Capacity request was MAPPED but not CONTEXTED: it fell into the Shipment catch-all,
    /// CapacityRequestContext stayed at its defaults, the handler ran at CorrelationId/TenantId/LegalEntityId
    /// all-zero and the request answered 500 INTERNAL_ERROR. The guard asserted that 500 so the known gap could
    /// not outlive its reason, and on 2026-10-12 it fired exactly as designed the moment the branch went in.
    ///
    /// It now asserts the thing it was standing in for — that the branch is in the pipeline — and it does so
    /// WITHOUT WRITING, which is forced rather than stylistic. Exactly one capacity plan can exist per scope
    /// for this fixture: CapacityRepository's duplicate check is (scope, horizon start, horizon end, demand
    /// plan id, demand plan version) and demand.IsExact pins all four to the fixture's values, so a second
    /// create answers 409 CAPACITY_PLAN_ALREADY_EXISTS whatever its scenario name. A creating probe here
    /// would therefore consume the single slot CapacityCorrelationTests needs, and measurement showed exactly
    /// that: with this test creating a plan, Correlation_survives_request_response_audit_and_event answered
    /// 409 on a shared host while passing when run alone. That is also why the original guard could assert
    /// 500 — asserting a refusal costs no slot.
    ///
    /// An unauthenticated request separates the pipelines without touching the slot, and the discriminator is
    /// the contract envelope rather than the status. Measured both ways on 2026-10-12: with the branch, the
    /// request is refused by CapacityContextMiddleware as 401 with the Capacity envelope's UNAUTHENTICATED and
    /// a Bearer challenge; with both lines of the fix disabled, the same request is refused as a 401 carrying
    /// NO BODY AND NO CODE at all — the Shipment catch-all returns early on an endpoint that has no Shipment
    /// permission attribute, and the authentication middleware refuses it bare. So the status matches either
    /// way and only the code says which middleware handled the path, which is why that is what this asserts.
    /// Scope population and the four-hop correlation chain belong to CapacityCorrelationTests, un-skipped
    /// with this branch, which is now the only test that creates; with the branch disabled those two answer
    /// 500 instead of 201 and 400, the original defect.
    /// </summary>
    [Fact]
    public async Task Capacity_controller_is_mapped_and_its_context_middleware_is_composed()
    {
        var reply = await _host.Send("POST", "/api/supply-chain/capacity-plans",
            SandopCapacityHttpHost.CapacityPlan(), token: null, "k-" + Guid.NewGuid());

        Assert.Equal(401, reply.Status);
        Assert.True(reply.Code == "UNAUTHENTICATED",
            "Capacity answered code '" + reply.Code + "' with status " + reply.Status + ". UNAUTHENTICATED is "
            + "CapacityContextMiddleware's refusal, and a bare 401 with no code is what the pipeline answers "
            + "when the Program.cs capacity branch is gone; measured on 2026-10-12. MOD-0192 :199/:200 rest "
            + "on this line. Body: '" + reply.Body + "'");

        // A Capacity refusal is a contract envelope, so the body carries the id the caller can quote back.
        // The sabotage measurement answered with an empty body, which this also rejects.
        Assert.False(string.IsNullOrWhiteSpace(reply.Correlation));
        Assert.Contains("UNAUTHENTICATED", reply.Body);
    }
}
