using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-FIX-R3T item 2 — the KR-B retry sweeper (<see cref="RecallQuarantineSweeper"/>) across tenants: one sweep sees the Pending requests of
/// every tenant and sends each inside ITS OWN tenant / company scope (the 0175 hold carries the request's tenant and company, never another
/// request's), and one request that throws does not end the sweep for the others. Real dispatcher, real MOD-0175 handler, the suite's own Mongo.
/// Two kinds of failure, two tests: a 0175 call that throws is caught by the DISPATCHER itself (the request stays Pending — not the sweeper's
/// catch); a row that throws in the sweeper's own loop (no company: <c>SetScope</c>) is the sweeper's per-request catch (CT decision A, s5).
/// </summary>
public sealed class RecallSweeperTenantTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallSweeperTenantTests(RecallApiFactory factory) => _factory = factory;

    private static readonly DateTimeOffset Later = DateTimeOffset.UtcNow + RecallQuarantineSweeper.PendingAge + TimeSpan.FromMinutes(1);

    /// <summary>A lot with 6 AVAILABLE, a case approved on it while the 0175 call crashes BEFORE it is made — one request left Pending.</summary>
    private async Task<(RecallScenario S, string Lot, BsonDocument Request)> PendingAsync(RecallScenario? sameTenant = null)
    {
        var s = sameTenant ?? new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        _factory.QuarantineCrash[lot] = "Before";
        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);
        var request = Assert.Single(RecallR3.Requests(s, caseId), r => r["LotId"].AsString == lot);
        Assert.Equal("Pending", request["Status"].AsString);
        return (s, lot, request);
    }

    private static BsonDocument Reload(RecallScenario s, BsonDocument request)
        => s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.QuarantineRequests).Find(new BsonDocument("_id", request["_id"])).Single();

    private static BsonDocument Hold(RecallScenario s, BsonValue holdId)
        => s.Factory.Database.GetCollection<BsonDocument>("quarantine_holds").Find(new BsonDocument("_id", holdId)).Single();

    [Fact]
    public async Task f_one_sweep_sends_each_tenants_request_in_its_own_tenant_and_company_scope()
    {
        var (s1, lot1, r1) = await PendingAsync();
        var (s2, lot2, r2) = await PendingAsync();
        Assert.NotEqual(s1.Tenant, s2.Tenant);
        Assert.NotEqual(s1.Ledger.LegalEntity, s2.Ledger.LegalEntity);
        _factory.QuarantineCrash.TryRemove(lot1, out _);
        _factory.QuarantineCrash.TryRemove(lot2, out _);

        var moved = await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s1.Tenant, s2.Tenant]);

        Assert.Equal(2, moved);
        foreach (var (s, request) in new[] { (s1, r1), (s2, r2) })
        {
            var after = Reload(s, request);
            Assert.Equal("Held", after["Status"].AsString);
            var hold = Hold(s, Assert.Single(after["HoldIds"].AsBsonArray));
            // the 0175 hold is written in the request's OWN tenant and company — the scope the sweeper built for THIS request
            Assert.Equal((request["TenantId"].ToString(), request["LegalEntityId"].ToString()), (hold["TenantId"].ToString(), hold["LegalEntityId"].ToString()));
            Assert.Equal((s.Tenant.ToString(), s.Ledger.LegalEntity.ToString()), (hold["TenantId"].ToString(), hold["LegalEntityId"].ToString()));
            Assert.Equal(6m, await s.Ledger.OnHandAsync(s.Ledger.LocationA, "QUARANTINE"));
        }
    }

    [Fact]
    public async Task g_the_dispatchers_own_catch_keeps_a_request_whose_0175_call_throws_Pending_and_the_others_are_sent()
    {
        var (s, lot1, r1) = await PendingAsync();
        var (_, lot2, r2) = await PendingAsync(s);
        var (_, lot3, r3) = await PendingAsync(s);
        _factory.QuarantineCrash.TryRemove(lot2, out _);
        _factory.QuarantineCrash.TryRemove(lot3, out _);
        // lot1 keeps its crash: its 0175 call throws again in the sweep

        try
        {
            var moved = await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s.Tenant]);

            Assert.Equal(2, moved);
            Assert.Equal("Pending", Reload(s, r1)["Status"].AsString);
            Assert.Equal("Held", Reload(s, r2)["Status"].AsString);
            Assert.Equal("Held", Reload(s, r3)["Status"].AsString);
        }
        finally
        {
            _factory.QuarantineCrash.TryRemove(lot1, out _);
        }
    }

    [Fact]
    public async Task g2_a_row_that_throws_in_the_sweep_loop_stays_Pending_is_logged_and_the_others_of_the_same_sweep_are_sent()
    {
        var (s, lot1, r1) = await PendingAsync();
        var (_, lot2, r2) = await PendingAsync(s);
        _factory.QuarantineCrash.TryRemove(lot1, out _);
        _factory.QuarantineCrash.TryRemove(lot2, out _);

        // a Pending row without a company, written straight into the collection: SetScope throws in the sweeper's own loop
        var broken = r1.DeepClone().AsBsonDocument;
        broken["_id"] = Guid.NewGuid().ToString();
        broken["QuarantineKey"] = r1["QuarantineKey"].AsString + ":broken";
        broken["NodeKey"] = r1["NodeKey"].AsString + ":broken";
        broken["LegalEntityId"] = BsonNull.Value;
        await s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.QuarantineRequests).InsertOneAsync(broken);

        var moved = await _factory.Services.GetRequiredService<RecallQuarantineSweeper>().SweepAsync(Later, CancellationToken.None, [s.Tenant]);

        Assert.Equal(2, moved);
        Assert.Equal("Held", Reload(s, r1)["Status"].AsString);
        Assert.Equal("Held", Reload(s, r2)["Status"].AsString);
        var left = Reload(s, broken);
        Assert.Equal("Pending", left["Status"].AsString);                                // the next sweep retries it
        Assert.Equal(broken["Attempts"], left["Attempts"]);                               // nothing was written for it
        var line = Assert.Single(_factory.Logs.Lines, l => l.Contains("recall.quarantine.sweep_request_failed", StringComparison.Ordinal)
                                                             && l.Contains(broken["_id"].AsString, StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("Error ", line, StringComparison.Ordinal);
        Assert.Contains($"tenant {s.Tenant}", line, StringComparison.OrdinalIgnoreCase);
    }
}
