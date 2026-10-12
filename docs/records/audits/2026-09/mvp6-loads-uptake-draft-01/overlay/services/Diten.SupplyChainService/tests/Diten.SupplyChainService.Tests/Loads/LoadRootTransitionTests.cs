using System.Net;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

// MOD-0185 finding F-1 (DRAFT, not built). Owner decision mvp6-loads-uptake-ph15-owner-decision-01.md,
// Decision 2: a transition on a Load without a stored authoritative root is rejected safely, with no
// contract or endpoint change. Existing code: 409 CORRELATION_ROOT_MISMATCH (loads-semantics-v3.1.0.md
// line 215; shipment-bundle.openapi.yaml transitionLoad '409'). A valid stored root keeps today's behaviour.
public sealed class LoadRootTransitionTests
{
    private static string TransitionPath(Guid id) => $"/api/shipment-bundle/loads/{id}/transition";

    private static void AssertRejected((HttpResponseMessage Response, JsonNode? Body) result)
    {
        Assert.Equal(HttpStatusCode.Conflict, result.Response.StatusCode);
        var body = Assert.IsType<JsonObject>(result.Body);
        Assert.Equal(new[] { "contractVersion", "error" }, body.Select(x => x.Key).Order().ToArray());
        Assert.Equal("CORRELATION_ROOT_MISMATCH", body["error"]!["code"]!.GetValue<string>());
        Assert.Equal(new[] { "code", "correlationId", "message" }, body["error"]!.AsObject().Select(x => x.Key).Order().ToArray());
    }

    [Fact]
    public async Task MissingStoredRootAndNilInboundCorrelationIsRejected()
    {
        await using var f = new LoadTestContext();
        var id = await LoadRootSeed.Create(f, "f1-create");
        await LoadRootSeed.UnsetRoot(f, id);
        var before = await f.Snapshot();
        var dependencyCalls = f.Calls.Values.Sum();

        var result = await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "f1-nil", correlation: LoadRootSeed.Nil);

        AssertRejected(result);
        Assert.Equal(before, await f.Snapshot());
        Assert.Equal(dependencyCalls, f.Calls.Values.Sum());
        var stored = await LoadRootSeed.Stored(f, id);
        Assert.False(stored.Contains("CorrelationRoot"));
        Assert.Equal("Draft", stored["Status"].AsString);

        // No receipt was stored, so the same key is evaluated again and rejected again.
        AssertRejected(await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "f1-nil", correlation: LoadRootSeed.Nil));
        Assert.Equal(before, await f.Snapshot());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("int32")]
    [InlineData("binary-uuid")]
    public async Task TransitionWithoutAuthoritativeStoredRootIsRejectedForEveryInboundCorrelation(string fixture)
    {
        await using var f = new LoadTestContext();
        var id = await LoadRootSeed.Create(f, "f1-create-" + fixture);
        switch (fixture)
        {
            case "missing": await LoadRootSeed.UnsetRoot(f, id); break;
            case "null": await LoadRootSeed.SetRoot(f, id, BsonNull.Value); break;
            case "malformed": await LoadRootSeed.SetRoot(f, id, "not-a-uuid"); break;
            case "int32": await LoadRootSeed.SetRoot(f, id, 7); break;
            case "binary-uuid": await LoadRootSeed.SetRoot(f, id, new BsonBinaryData(Guid.Parse(f.Root), GuidRepresentation.Standard)); break;
        }
        var before = await f.Snapshot();
        var dependencyCalls = f.Calls.Values.Sum();

        var inbound = new[] { LoadRootSeed.Nil, f.Root, Guid.NewGuid().ToString() };
        for (var i = 0; i < inbound.Length; i++)
            AssertRejected(await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), $"f1-{fixture}-{i}", correlation: inbound[i]));

        Assert.Equal(before, await f.Snapshot());
        Assert.Equal(dependencyCalls, f.Calls.Values.Sum());
    }

    [Fact]
    public async Task ValidStoredRootKeepsExistingTransitionBehaviour()
    {
        await using var f = new LoadTestContext();
        var id = await LoadRootSeed.Create(f, "valid-create");
        var before = await f.Snapshot();

        AssertRejected(await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "valid-other-root", correlation: Guid.NewGuid().ToString()));
        Assert.Equal(before, await f.Snapshot());

        var ok = await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "valid-ok");
        Assert.Equal(HttpStatusCode.OK, ok.Response.StatusCode);
        Assert.Equal("Planned", ok.Body!["status"]!.GetValue<string>());
        Assert.False(ok.Body["idempotentReplay"]!.GetValue<bool>());
        var stored = await LoadRootSeed.Stored(f, id);
        Assert.Equal("Planned", stored["Status"].AsString);
        Assert.Equal(Guid.Parse(f.Root), Guid.Parse(stored["CorrelationRoot"].AsString));
        Assert.Equal(2, await f.Count("loads_receipts"));
        Assert.Equal(2, await f.Count("loads_audit"));
        Assert.Equal(2, await f.Count("loads_outbox"));

        var replay = await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "valid-ok");
        Assert.Equal(HttpStatusCode.OK, replay.Response.StatusCode);
        Assert.True(replay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(2, await f.Count("loads_audit"));
    }

    [Fact]
    public async Task StoredNilRootIsAPresentRootAndMatchesNilInboundCorrelation()
    {
        await using var f = new LoadTestContext();
        f.Root = LoadRootSeed.Nil; // nil correlation is valid (loads-semantics-v3.1.0.md line 178)
        var id = await LoadRootSeed.Create(f, "nil-create");
        Assert.Equal(LoadRootSeed.Nil, (await LoadRootSeed.Stored(f, id))["CorrelationRoot"].AsString);

        var listed = LoadRootSeed.Items((await f.Send("GET", LoadRootSeed.ListPath)).Body!);
        Assert.Equal(LoadRootSeed.Nil, listed[id]!.GetValue<string>());

        var ok = await f.Send("POST", TransitionPath(id), f.TransitionBody("Planned"), "nil-ok");
        Assert.Equal(HttpStatusCode.OK, ok.Response.StatusCode);
        Assert.Equal("Planned", ok.Body!["status"]!.GetValue<string>());
        Assert.Equal(LoadRootSeed.Nil, (await LoadRootSeed.Stored(f, id))["CorrelationRoot"].AsString);
    }
}
