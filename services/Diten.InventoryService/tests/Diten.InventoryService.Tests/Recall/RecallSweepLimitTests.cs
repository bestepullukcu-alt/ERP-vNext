using Diten.InventoryService.Application.Features.Recall.Quarantine;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — FU-0177-SWEEP-01: a Pending recall quarantine request that keeps ending without an answer is COUNTED per sweep
/// (<c>DeferredAttempts</c>) and at the configured limit turns <c>Failed</c> with the reason (event + audit, like every outcome); the sweeper
/// never takes a Failed request again. FU-0177-SWEEP-02: the dispatcher's own warning names the tenant and the company (OBS-001), no person data.
/// Real dispatcher, real MOD-0175 handler, the suite's own Mongo; this class owns its factory (the limit is set to 3 here).
/// </summary>
public sealed class RecallSweepLimitTests : IClassFixture<RecallApiFactory>
{
    private const int Limit = 3;
    private readonly RecallApiFactory _factory;

    public RecallSweepLimitTests(RecallApiFactory factory)
    {
        _factory = factory;
        _factory.Services.GetRequiredService<IOptions<RecallQuarantineSweepOptions>>().Value.MaxDeferredAttempts = Limit;
    }

    private static DateTimeOffset Sweep(int n) => DateTimeOffset.UtcNow + (RecallQuarantineSweeper.PendingAge + TimeSpan.FromMinutes(1)) * (n + 1);

    private RecallQuarantineSweeper Sweeper => _factory.Services.GetRequiredService<RecallQuarantineSweeper>();

    private async Task<(RecallScenario S, string Lot, string CaseId, BsonDocument Request)> PendingAsync()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        _factory.QuarantineCrash[lot] = "Before"; // the 0175 call throws, every time
        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);
        var request = Assert.Single(RecallR3.Requests(s, caseId), r => r["LotId"].AsString == lot);
        Assert.Equal("Pending", request["Status"].AsString);
        return (s, lot, caseId, request);
    }

    private static BsonDocument Reload(RecallScenario s, BsonValue id)
        => s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.QuarantineRequests).Find(new BsonDocument("_id", id)).Single();

    [Fact]
    public void The_limit_is_a_setting_with_a_sane_default()
    {
        Assert.Equal("Inventory:Recall:QuarantineSweep", RecallQuarantineSweepOptions.SectionName);
        Assert.Equal(10, new RecallQuarantineSweepOptions().MaxDeferredAttempts);
        Assert.Equal(Limit, Sweeper.MaxDeferredAttempts); // bound through DI (the hosted service reads the same singleton)
    }

    [Fact]
    public async Task A_request_without_an_answer_is_counted_turns_Failed_with_its_reason_at_the_limit_and_is_never_taken_again()
    {
        var (s, lot, caseId, request) = await PendingAsync();
        try
        {
            for (var n = 0; n < Limit - 1; n++)
            {
                await Sweeper.SweepAsync(Sweep(n), CancellationToken.None, [s.Tenant]);
                var mid = Reload(s, request["_id"]);
                Assert.Equal(("Pending", n + 1), (mid["Status"].AsString, mid["DeferredAttempts"].AsInt32));
            }

            await Sweeper.SweepAsync(Sweep(Limit - 1), CancellationToken.None, [s.Tenant]);
            var failed = Reload(s, request["_id"]);
            Assert.Equal(("Failed", Limit), (failed["Status"].AsString, failed["DeferredAttempts"].AsInt32));
            Assert.Equal(RecallQuarantineDispatcher.RetryLimitReached, failed["ErrorCode"].AsString);
            Assert.Contains($"No answer after {Limit} sweeps (limit {Limit})", failed["ErrorMessage"].AsString, StringComparison.Ordinal);
            Assert.Contains("Exception", failed["ErrorMessage"].AsString, StringComparison.Ordinal);            // the reason names what failed
            Assert.Equal(request["Attempts"], failed["Attempts"]);                                             // no 0175 call was counted as made
            // the outcome is written like every other: a QuarantineFailed case event and its audit row
            Assert.Contains(s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.CaseEvents)
                .Find(new BsonDocument { ["CaseId"] = caseId, ["Kind"] = "QuarantineFailed" }).ToList(), e => e["Text"].AsString.StartsWith("Failed:", StringComparison.Ordinal));
            Assert.Contains(_factory.Logs.Lines, l => l.Contains("recall.quarantine.retry_limit", StringComparison.Ordinal)
                                                       && l.Contains($"tenant {s.Tenant}", StringComparison.OrdinalIgnoreCase));

            // never taken again: more sweeps change nothing
            await Sweeper.SweepAsync(Sweep(Limit + 2), CancellationToken.None, [s.Tenant]);
            var later = Reload(s, request["_id"]);
            Assert.Equal(("Failed", Limit, failed["Version"]), (later["Status"].AsString, later["DeferredAttempts"].AsInt32, later["Version"]));
        }
        finally
        {
            _factory.QuarantineCrash.TryRemove(lot, out _);
        }
    }

    [Fact]
    public async Task A_row_that_cannot_get_a_scope_is_counted_too_and_turns_Failed_at_the_limit()
    {
        var (s, lot, _, request) = await PendingAsync();
        _factory.QuarantineCrash.TryRemove(lot, out _);
        var broken = request.DeepClone().AsBsonDocument;
        broken["_id"] = Guid.NewGuid().ToString();
        broken["QuarantineKey"] = request["QuarantineKey"].AsString + ":broken";
        broken["NodeKey"] = request["NodeKey"].AsString + ":broken";
        broken["LegalEntityId"] = BsonNull.Value;                                                             // SetScope throws in the sweep loop
        await s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.QuarantineRequests).InsertOneAsync(broken);

        for (var n = 0; n < Limit; n++) await Sweeper.SweepAsync(Sweep(n), CancellationToken.None, [s.Tenant]);

        var left = Reload(s, broken["_id"]);
        Assert.Equal(("Failed", Limit), (left["Status"].AsString, left["DeferredAttempts"].AsInt32));
        Assert.Equal(RecallQuarantineDispatcher.RetryLimitReached, left["ErrorCode"].AsString);
        Assert.Contains("last: ", left["ErrorMessage"].AsString, StringComparison.Ordinal);
        Assert.Equal("Held", Reload(s, request["_id"])["Status"].AsString);                                   // the sound row of the same case was sent
        await Sweeper.SweepAsync(Sweep(Limit + 2), CancellationToken.None, [s.Tenant]);
        Assert.Equal(Limit, Reload(s, broken["_id"])["DeferredAttempts"].AsInt32);                            // not taken again
    }

    [Fact]
    public async Task The_dispatchers_warning_names_the_tenant_and_the_company_and_no_person()
    {
        var (s, lot, _, request) = await PendingAsync();
        try
        {
            await Sweeper.SweepAsync(Sweep(0), CancellationToken.None, [s.Tenant]);
            // the approval's own dispatch and the sweep both warn: every such line carries the tenant and the company, and no user id
            var lines = _factory.Logs.Lines.Where(l => l.Contains("recall.quarantine.dispatch_deferred", StringComparison.Ordinal)
                                                      && l.Contains(request["_id"].AsString, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Equal(2, lines.Count);
            Assert.All(lines, line =>
            {
                Assert.StartsWith("Warning ", line, StringComparison.Ordinal);
                Assert.Contains($"tenant {s.Tenant}", line, StringComparison.OrdinalIgnoreCase);
                Assert.Contains($"company {s.Ledger.LegalEntity}", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(s.Qa1.ToString(), line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(s.Qa2.ToString(), line, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            _factory.QuarantineCrash.TryRemove(lot, out _);
        }
    }
}
