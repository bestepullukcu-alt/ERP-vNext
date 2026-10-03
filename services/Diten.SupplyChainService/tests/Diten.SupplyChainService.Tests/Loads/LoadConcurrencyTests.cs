using System.Net;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadConcurrencyTests
{
    [Fact]
    public async Task CompetingCreatesForOneShipmentCommitExactlyOneOwner()
    {
        await using var f = new LoadTestContext();
        var left = f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "race-left");
        var right = f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "race-right");
        var results = await Task.WhenAll(left, right);

        Assert.Single(results, x => x.Response.StatusCode == HttpStatusCode.Created);
        var rejected = Assert.Single(results, x => x.Response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("SHIPMENT_ALREADY_ASSIGNED", rejected.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("load_assignments"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task ConcurrentSameKeyRequestsProduceOneCommitAndOneDurableReplay()
    {
        await using var f = new LoadTestContext();
        var body = f.CreateBody();
        var first = f.Send("POST", "/api/shipment-bundle/loads", body, "same-key-race");
        var second = f.Send("POST", "/api/shipment-bundle/loads", body, "same-key-race");
        var results = await Task.WhenAll(first, second);
        Assert.All(results, x => Assert.Equal(HttpStatusCode.Created, x.Response.StatusCode));
        Assert.Single(results, x => x.Body!["idempotentReplay"]!.GetValue<bool>() is false);
        Assert.Single(results, x => x.Body!["idempotentReplay"]!.GetValue<bool>() is true);
        Assert.Equal(results[0].Body!["loadId"]!.GetValue<string>(), results[1].Body!["loadId"]!.GetValue<string>());
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("load_assignments"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task RetryAfterCancellationNeverDeletesNewOwnersAssignment()
    {
        await using var f = new LoadTestContext();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "first");
        var firstId = create.Body!["loadId"]!.GetValue<string>();
        var cancel = await f.Send("POST", $"/api/shipment-bundle/loads/{firstId}/transition", f.TransitionBody("Cancelled"), "cancel");
        Assert.Equal(HttpStatusCode.OK, cancel.Response.StatusCode);
        var second = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "second");
        Assert.Equal(HttpStatusCode.Created, second.Response.StatusCode);
        var staleReplay = await f.Send("POST", $"/api/shipment-bundle/loads/{firstId}/transition", f.TransitionBody("Cancelled"), "cancel");
        Assert.True(staleReplay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(1, await f.Count("load_assignments"));
        Assert.Equal(second.Body!["loadId"]!.GetValue<string>(),
            (await f.Db.GetCollection<MongoDB.Bson.BsonDocument>("load_assignments").Find(f.Scope()).SingleAsync())["LoadId"].AsString);
    }

    [Fact]
    public async Task ConcurrentPlannedAndCancelledTransitionsProduceOnlyASerialLifecycle()
    {
        await using var f = new LoadTestContext();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "transition-race-create");
        var id = create.Body!["loadId"]!.GetValue<string>();
        var planned = f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Planned"), "transition-race-planned");
        var cancelled = f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Cancelled"), "transition-race-cancelled");
        var outcomes = await Task.WhenAll(planned, cancelled);
        Assert.All(outcomes, x => Assert.Contains((int)x.Response.StatusCode, new[] { 200, 422 }));
        var applied = outcomes.Count(x => (int)x.Response.StatusCode == 200);
        Assert.InRange(applied, 1, 2);
        var aggregate = await f.Db.GetCollection<Diten.SupplyChainService.Domain.Features.Loads.LoadPlan>("loads").Find(f.Scope()).SingleAsync();
        if (applied == 2) Assert.Equal("Cancelled", aggregate.Status.ToString());
        else Assert.Contains(aggregate.Status.ToString(), new[] { "Planned", "Cancelled" });
        Assert.Equal(applied + 1, await f.Count("loads_audit"));
        Assert.Equal(applied + 1, await f.Count("loads_outbox"));
        Assert.All(await f.Db.GetCollection<MongoDB.Bson.BsonDocument>("loads_outbox").Find(f.Scope()).ToListAsync(),
            x => Assert.Equal("Pending", x["Status"].AsString));
    }
}
