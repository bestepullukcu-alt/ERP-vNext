using Diten.SupplyChainService.Domain.Features.Loads;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadLifecycleTests
{
    private static readonly string[] States = ["Draft", "Planned", "Tendered", "Accepted", "Dispatched", "Completed", "Cancelled"];
    private static readonly Dictionary<string, string[]> Paths = new()
    {
        ["Draft"] = [],
        ["Planned"] = ["Planned"],
        ["Tendered"] = ["Planned", "Tendered"],
        ["Accepted"] = ["Planned", "Tendered", "Accepted"],
        ["Dispatched"] = ["Planned", "Tendered", "Accepted", "Dispatched"],
        ["Completed"] = ["Planned", "Tendered", "Accepted", "Dispatched", "Completed"],
        ["Cancelled"] = ["Cancelled"]
    };

    [Fact]
    public async Task EverySourceTargetPairMatchesFrozenLifecycleAndRejectedPairsAreAtomic()
    {
        var allowed = new HashSet<(string From, string To)>
        {
            ("Draft", "Planned"), ("Draft", "Cancelled"),
            ("Planned", "Tendered"), ("Planned", "Cancelled"),
            ("Tendered", "Accepted"), ("Tendered", "Cancelled"),
            ("Accepted", "Dispatched"), ("Accepted", "Cancelled"),
            ("Dispatched", "Completed")
        };
        foreach (var from in States)
        foreach (var to in States)
        {
            await using var f = new LoadTestContext();
            var shipment = Guid.NewGuid();
            var created = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(shipment), "create");
            Assert.Equal(201, (int)created.Response.StatusCode);
            var id = Guid.Parse(created.Body!["loadId"]!.GetValue<string>());
            var step = 0;
            foreach (var target in Paths[from])
            {
                var setup = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody(target), "setup-" + step++);
                Assert.Equal(200, (int)setup.Response.StatusCode);
            }
            var before = await f.Snapshot();
            var result = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody(to), $"edge-{from}-{to}");
            if (allowed.Contains((from, to)))
            {
                Assert.Equal(200, (int)result.Response.StatusCode);
                Assert.Equal(to, result.Body!["status"]!.GetValue<string>());
                var aggregate = await f.Db.GetCollection<LoadPlan>("loads").Find(f.Scope()).SingleAsync();
                Assert.Equal(to, aggregate.Status.ToString());
                Assert.Equal(Paths[from].Length + 2, aggregate.Version); // Create plus setup edges plus this edge.
                Assert.Equal(Paths[from].Length + 2, await f.Count("loads_audit"));
            }
            else
            {
                Assert.Equal(422, (int)result.Response.StatusCode);
                Assert.Equal("INVALID_LOAD_TRANSITION", result.Body!["error"]!["code"]!.GetValue<string>());
                Assert.Equal(before, await f.Snapshot());
            }
        }
    }
}
