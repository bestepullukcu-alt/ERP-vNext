using Xunit;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// Gate 18.0 row 13 for MOD-0186 and MOD-0187. The row asks that service logs carry operation, result,
/// correlation and replay (MOD-0185 pack :432 words it; the Returns and Claims packs take the same shape).
/// Those four live on the controllers' outcome lines — ReturnsController.cs:26 and ClaimsController.cs:28 —
/// and only a request reaches them, which is why neither module could evidence the row from its own suite:
/// ReturnReplayTests and ClaimReplayTests drive repositories.
///
/// This class exists to produce that traffic over HTTP and to assert the HTTP contract of it. The log lines
/// themselves are measured from the run output into the lane record, the same way MOD-0184 and MOD-0185 were,
/// because a test cannot read the host's Serilog sink and an assertion that pretended to would be measuring
/// its own fixture. What the test does assert is the part that makes the log meaningful: that a replay really
/// is a replay (same id back, idempotentReplay true) and that the correlation the caller sent is the one that
/// comes back — if either were false, a log line carrying them would be carrying nothing.
/// </summary>
[Collection("Returns Claims Http")]
public sealed class ReturnsClaimsObservabilityTests(ReturnsClaimsHttpHost host)
{
    private const string Returns = "/api/shipment-bundle/returns";
    private const string Claims = "/api/shipment-bundle/claims";

    [Fact]
    public async Task Return_create_replay_transition_and_query_all_answer_over_http()
    {
        var correlation = Guid.NewGuid().ToString();
        var key = "row13-return-" + Guid.NewGuid();

        var created = await host.Send("POST", Returns, host.ReturnBody("10"), host.Token(), key, correlation);
        Assert.True(created.Status == 201, "create answered " + created.Status + ": " + created.Body);
        Assert.Equal(correlation, created.Correlation);
        Assert.False(created.Replay);
        var returnId = created.Json!["returnId"]!.GetValue<string>();

        // The same key again: the response must be the first one, flagged as a replay.
        var replayed = await host.Send("POST", Returns, host.ReturnBody("10"), host.Token(), key, correlation);
        Assert.Equal(201, replayed.Status);
        Assert.True(replayed.Replay, "second create with the same key was not flagged a replay: " + replayed.Body);
        Assert.Equal(returnId, replayed.Json!["returnId"]!.GetValue<string>());

        // The same correlation as the create, and that is a contract fact rather than tidiness: the
        // correlation id IS the correlation root the aggregate was written under, so a transition sent under
        // a fresh one answers 409 CORRELATION_ROOT_MISMATCH. Measured here with a new Guid before it was
        // measured with this one.
        var transition = await host.Send("POST", Returns + "/" + returnId + "/transition",
            new System.Text.Json.Nodes.JsonObject
            {
                ["targetStatus"] = "Authorized",
                ["occurredAt"] = "2026-09-20T09:00:00.123456789Z"
            }, host.Token(), "row13-return-transition-" + Guid.NewGuid(), correlation);
        Assert.True(transition.Status == 200, "transition answered " + transition.Status + ": " + transition.Body);
        Assert.Equal(correlation, transition.Correlation);

        var listed = await host.Send("GET", Returns, null, host.Token(), null, Guid.NewGuid().ToString());
        Assert.Equal(200, listed.Status);
    }

    [Fact]
    public async Task Claim_create_replay_and_query_all_answer_over_http()
    {
        var correlation = Guid.NewGuid().ToString();
        var key = "row13-claim-" + Guid.NewGuid();

        var created = await host.Send("POST", Claims, host.ClaimBody(), host.Token(), key, correlation);
        Assert.True(created.Status == 201, "create answered " + created.Status + ": " + created.Body);
        Assert.Equal(correlation, created.Correlation);
        Assert.False(created.Replay);
        var claimId = created.Json!["claimId"]!.GetValue<string>();

        var replayed = await host.Send("POST", Claims, host.ClaimBody(), host.Token(), key, correlation);
        Assert.Equal(201, replayed.Status);
        Assert.True(replayed.Replay, "second create with the same key was not flagged a replay: " + replayed.Body);
        Assert.Equal(claimId, replayed.Json!["claimId"]!.GetValue<string>());

        var listed = await host.Send("GET", Claims, null, host.Token(), null, Guid.NewGuid().ToString());
        Assert.Equal(200, listed.Status);
    }
}
