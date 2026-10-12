using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadAtomicityTests
{
    private static async Task ConfigureCommitFailures(LoadTestContext f, int times)
    {
        await f.Db.Client.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument
        {
            { "configureFailPoint", "failCommand" },
            { "mode", new MongoDB.Bson.BsonDocument("times", times) },
            { "data", new MongoDB.Bson.BsonDocument
                {
                    { "failCommands", new MongoDB.Bson.BsonArray { "commitTransaction" } },
                    { "errorCode", 91 },
                    { "errorLabels", new MongoDB.Bson.BsonArray { "UnknownTransactionCommitResult" } }
                }
            }
        });
    }

    private static async Task DisableCommitFailures(LoadTestContext f) =>
        await f.Db.Client.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });

    [Fact]
    public async Task UnknownCommitResultRetriesAndCommitsExactlyOnce()
    {
        await using var f = new LoadTestContext();
        await ConfigureCommitFailures(f, 1);
        (HttpResponseMessage Response, System.Text.Json.Nodes.JsonNode? Body) response;
        try { response = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "unknown-commit-once"); }
        finally { await DisableCommitFailures(f); }
        Assert.Equal(HttpStatusCode.Created, response.Response.StatusCode);
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task UnresolvedUnknownCommitReturns503WithoutPretendingRollbackOrSuccess()
    {
        await using var f = new LoadTestContext();
        await ConfigureCommitFailures(f, 3);
        (HttpResponseMessage Response, System.Text.Json.Nodes.JsonNode? Body) response;
        try { response = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "unknown-commit-exhausted"); }
        finally { await DisableCommitFailures(f); }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.Response.StatusCode);
        Assert.Equal("PERSISTENCE_UNAVAILABLE", response.Body!["error"]!["code"]!.GetValue<string>());
        Assert.True(await f.Count("loads") is 0 or 1);
        Assert.True(await f.Count("loads_receipts") is 0 or 1);
        Assert.True(await f.Count("loads_audit") is 0 or 1);
        Assert.True(await f.Count("loads_outbox") is 0 or 1);
        var counts = new[] { await f.Count("loads"), await f.Count("loads_receipts"), await f.Count("loads_audit"), await f.Count("loads_outbox") };
        Assert.True(counts.All(x => x == 0) || counts.All(x => x == 1));
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("entity")]
    [InlineData("receipt")]
    [InlineData("audit")]
    [InlineData("event")]
    [InlineData("beforeCommit")]
    public async Task FailureBeforeCommitRollsBackAllFiveCollectionsAndKeyCanRetry(string phase)
    {
        await using var f = new LoadTestContext();
        var before = await f.Snapshot();
        f.Probe.FailAt = phase;
        var body = f.CreateBody();
        var failed = await f.Send("POST", "/api/shipment-bundle/loads", body, "fault-key");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.Response.StatusCode);
        Assert.Null(f.Probe.FailAt);
        Assert.Equal(before, await f.Snapshot());
        var success = await f.Send("POST", "/api/shipment-bundle/loads", body, "fault-key");
        Assert.True(success.Response.StatusCode == HttpStatusCode.Created,
            $"Retry after {phase} failed: {(int)success.Response.StatusCode} {success.Body}");
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("load_assignments"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task CancellationReleaseAndAggregateAuditReceiptEventCommitTogether()
    {
        await using var f = new LoadTestContext();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "create");
        var id = create.Body!["loadId"]!.GetValue<string>();
        f.Probe.FailAt = "assignment";
        var before = await f.Snapshot();
        var failed = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Cancelled"), "cancel");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.Response.StatusCode);
        Assert.Equal(before, await f.Snapshot());
        var cancelled = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Cancelled"), "cancel");
        Assert.Equal(HttpStatusCode.OK, cancelled.Response.StatusCode);
        Assert.Equal(0, await f.Count("load_assignments"));
        Assert.Equal(2, await f.Count("loads_receipts"));
        Assert.Equal(2, await f.Count("loads_audit"));
        Assert.Equal(2, await f.Count("loads_outbox"));
        var outbox = await f.Db.GetCollection<BsonDocument>("loads_outbox").Find(f.Scope()).ToListAsync();
        Assert.All(outbox, row => Assert.Equal("Pending", row["Status"].AsString));
        Assert.Equal(2, outbox.Select(x => x["EventId"].AsString).Distinct().Count());
        Assert.All(outbox, row => Assert.Equal(f.Root, row["Envelope"]["correlationId"].AsString));
    }

    [Fact]
    public async Task AcknowledgedCommitLossRetriesFromDurableReceiptWithoutDuplicateWrites()
    {
        await using var f = new LoadTestContext();
        f.Probe.FailAt = "afterCommit";
        var body = f.CreateBody();
        var uncertain = await f.Send("POST", "/api/shipment-bundle/loads", body, "commit-key");
        Assert.Equal(HttpStatusCode.InternalServerError, uncertain.Response.StatusCode);
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
        var retry = await f.Send("POST", "/api/shipment-bundle/loads", body, "commit-key");
        Assert.Equal(HttpStatusCode.Created, retry.Response.StatusCode);
        Assert.True(retry.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }
}
