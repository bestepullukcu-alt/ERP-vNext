using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-NAMES1 — RECALL 1.8.0 on the drills: the run starter DID the action → named at the time of it (the drill events keep no name, so the
/// queue row of the start correlation and the same user names it); the plan owner is ASSIGNED → never named by the service, even when the
/// owner's own queue rows carry a name (the screen shows the current account label).
/// </summary>
public sealed class RecallDrillNamesTests : IClassFixture<RecallDrillApiFactory>
{
    private const string Ayse = "Ayşe Demir";
    private readonly RecallDrillApiFactory _factory;

    public RecallDrillNamesTests(RecallDrillApiFactory factory) => _factory = factory;

    private void NameInQueue(DrillWorld w, Guid user, string name)
        => Assert.True(_factory.Database.GetCollection<BsonDocument>("central_audit_outbox").UpdateMany(
            new BsonDocument { ["TenantId"] = w.Tenant.ToString("D"), ["ActorId"] = user.ToString("D") },
            new BsonDocument("$set", new BsonDocument("ActorDisplayName", name))).ModifiedCount > 0);

    [Fact]
    public async Task The_run_starter_is_named_and_the_assigned_plan_owner_is_not()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        await w.FinishedGoodAsync();
        var plan = await w.PlanAsync();                                   // owner = Qa1, created by Qa1 (Runner)
        var run = await w.StartOkAsync(plan.GetProperty("planId").GetString()!);
        NameInQueue(w, w.S.Qa1, Ayse);

        var read = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/drill-runs/{run.GetProperty("runId").GetString()}", w.Runner, null, 200);
        Assert.Equal(Ayse, read.GetProperty("startedBy").GetProperty("name").GetString());
        var runs = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-runs?page=1&pageSize=20", w.Runner, null, 200);
        Assert.Equal(Ayse, runs.GetProperty("items")[0].GetProperty("startedBy").GetProperty("name").GetString());

        var plans = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/drill-plans", w.Runner, null, 200);
        var owner = (plans.ValueKind == JsonValueKind.Array ? plans : plans.GetProperty("items")).EnumerateArray()
            .Single(p => p.GetProperty("planId").GetString() == plan.GetProperty("planId").GetString()).GetProperty("owner");
        Assert.Equal(w.S.Qa1.ToString("D"), owner.GetProperty("userId").GetString());
        Assert.False(owner.TryGetProperty("name", out _), "an assigned person is never named by the service");
    }
}
