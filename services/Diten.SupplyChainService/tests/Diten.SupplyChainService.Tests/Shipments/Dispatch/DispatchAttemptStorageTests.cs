using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Shipments.Dispatch;

// MOD-0183 DL-1 (plan r3 §4 step 2): the stored attempt and its conditional writes, against the owned replica set.
// Every attempt write is filtered on tenant, legal entity, attempt number and lease id (§23.4 "Sent marker and fencing").
public sealed class DispatchAttemptStorageTests
{
    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
    private readonly ShipmentScope _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    private readonly IMongoDatabase _db = new MongoClient(Connection).GetDatabase("diten_mod0183_dispatch_storage");
    private ShipmentRepository Repository() => new(_db, new NoOpShipmentCommitProbe());

    private async Task<Shipment> Seed(ShipmentStatus status = ShipmentStatus.Planned)
    {
        var s = new Shipment
        {
            Id = Guid.NewGuid(), TenantId = _scope.TenantId, LegalEntityId = _scope.LegalEntityId, CreatedBy = _scope.ActorId, CreatedAt = DateTimeOffset.UtcNow,
            Version = 2, ShipmentNumber = "SHP-T", SourceModule = "MOD-0141", SourceType = "SALES_ORDER", SourceDocumentId = "SO-1",
            WarehouseReferenceId = "wh-01", ShipToReference = "C-1", PlannedShipAt = DateTimeOffset.UtcNow, Status = status, CorrelationId = Guid.NewGuid(),
            Lines = [new ShipmentLine("1", Guid.NewGuid(), Guid.NewGuid(), "3", "EA", null) { SkuLevel = "Lsku", LotId = "lot-1" }]
        };
        await _db.GetCollection<Shipment>("sce_shipments").InsertOneAsync(s);
        return s;
    }
    private static DispatchAttempt Attempt(int n = 1) => new()
    {
        N = n, State = DispatchState.InProgress, OccurredAt = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero),
        TransactionDate = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero), PostingDate = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero),
        LeaseId = Guid.NewGuid(), LeaseUntil = DateTimeOffset.UtcNow.AddSeconds(40), CorrelationId = Guid.NewGuid(),
        Lines = [new DispatchAttemptLine { LineNumber = "1" }]
    };

    [Fact]
    public async Task BeginIsOneWinnerAndOnlyOnAPlannedShipment()
    {
        var repo = Repository(); var s = await Seed();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, null, Attempt(), default)));
        Assert.Single(results, r => r);
        var stored = (await repo.GetAsync(_scope, s.Id, default))!;
        Assert.Equal(DispatchState.InProgress, stored.DispatchAttempt!.State);
        Assert.Equal("InProgress", stored.DispatchStateWire()); Assert.False(stored.StockIssued());
        // Resume is conditional on the observed lease: a stale observation loses.
        var stale = Attempt(); Assert.False(await repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, stale, Attempt(), default));
        var resumed = stored.DispatchAttempt.Copy(); resumed.LeaseId = Guid.NewGuid();
        Assert.True(await repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, stored.DispatchAttempt, resumed, default));
        var dispatched = await Seed(ShipmentStatus.Dispatched);
        Assert.False(await repo.TryBeginOrResumeDispatchAsync(_scope, dispatched.Id, null, Attempt(), default));
        var foreign = _scope with { LegalEntityId = Guid.NewGuid() };
        var other = await Seed();
        Assert.False(await repo.TryBeginOrResumeDispatchAsync(foreign, other.Id, null, Attempt(), default));
    }

    [Fact]
    public async Task SaveNeedsTheHeldAttemptAndLeaseAndRoundTripsEveryField()
    {
        var repo = Repository(); var s = await Seed(); var a = Attempt();
        Assert.True(await repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, null, a, default));
        var held = a.LeaseId;
        a.Lines[0].Sent = true; a.Lines[0].RequestJson = "{\"idempotencyKey\":\"k\"}"; a.Lines[0].IssueTransactionId = "T1"; a.Lines[0].LotId = "lot-1";
        a.ReversalDate = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        Assert.False(await repo.SaveDispatchAttemptAsync(_scope, s.Id, a, Guid.NewGuid(), default));
        var wrongN = a.Copy(); wrongN.N = 2; Assert.False(await repo.SaveDispatchAttemptAsync(_scope, s.Id, wrongN, held, default));
        Assert.Null((await repo.GetAsync(_scope, s.Id, default))!.DispatchAttempt!.Lines[0].IssueTransactionId);
        Assert.True(await repo.SaveDispatchAttemptAsync(_scope, s.Id, a, held, default));
        var stored = (await repo.GetAsync(_scope, s.Id, default))!.DispatchAttempt!;
        Assert.Equal(("T1", "lot-1", true, "{\"idempotencyKey\":\"k\"}"), (stored.Lines[0].IssueTransactionId, stored.Lines[0].LotId, stored.Lines[0].Sent, stored.Lines[0].RequestJson));
        Assert.Equal(a.OccurredAt, stored.OccurredAt); Assert.Equal(a.ReversalDate, stored.ReversalDate); Assert.Equal(held, stored.LeaseId);
        Assert.False(await repo.SaveDispatchAttemptAsync(_scope with { TenantId = Guid.NewGuid() }, s.Id, a, held, default));
    }

    [Fact]
    public async Task CompensatedAttemptMovesToHistoryAndTheNextAttemptIsNPlusOne()
    {
        var repo = Repository(); var s = await Seed(); var a = Attempt();
        Assert.True(await repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, null, a, default));
        a.Lines[0].IssueTransactionId = "T1"; a.Lines[0].ReversalTransactionId = "R1";
        Assert.False(await repo.CloseCompensatedAttemptAsync(_scope, s.Id, a, Guid.NewGuid(), default));
        Assert.True(await repo.CloseCompensatedAttemptAsync(_scope, s.Id, a, a.LeaseId, default));
        var stored = (await repo.GetAsync(_scope, s.Id, default))!;
        Assert.Null(stored.DispatchAttempt); Assert.Single(stored.DispatchHistory);
        Assert.Equal((DispatchState.Compensated, "T1", "R1"), (stored.DispatchHistory[0].State, stored.DispatchHistory[0].Lines[0].IssueTransactionId, stored.DispatchHistory[0].Lines[0].ReversalTransactionId));
        Assert.Equal(2, stored.NextDispatchAttemptNumber()); Assert.False(stored.StockIssued()); Assert.Equal("None", stored.DispatchStateWire());
        await repo.AppendLateWriteAsync(_scope, s.Id, new DispatchLateWrite(1, "1", "T9", "R9", "Reversed", DateTimeOffset.UtcNow), default);
        Assert.Equal("T9", (await repo.GetAsync(_scope, s.Id, default))!.DispatchLateWrites.Single().IssueTransactionId);
    }

    [Fact]
    public async Task DropRemovesOnlyTheHeldInProgressAttempt()
    {
        var repo = Repository(); var s = await Seed(); var a = Attempt();
        Assert.True(await repo.TryBeginOrResumeDispatchAsync(_scope, s.Id, null, a, default));
        Assert.False(await repo.DropUnpostedAttemptAsync(_scope, s.Id, 1, Guid.NewGuid(), default));
        Assert.True(await repo.DropUnpostedAttemptAsync(_scope, s.Id, 1, a.LeaseId, default));
        Assert.Null((await repo.GetAsync(_scope, s.Id, default))!.DispatchAttempt);
    }

    // A shipment stored before DL-1 has none of the new fields: it reads, has no attempt (stockIssued null, the MOD-0186
    // pre-WP-P1 marker) and its next attempt is 1.
    [Fact]
    public async Task PreDl1DocumentReadsWithoutAttempt()
    {
        var raw = new BsonDocument
        {
            { "_id", Guid.NewGuid().ToString() }, { "TenantId", _scope.TenantId.ToString() }, { "LegalEntityId", _scope.LegalEntityId.ToString() }, { "IsDeleted", false },
            { "ShipmentNumber", "S-OLD" }, { "SourceModule", "MOD-0141" }, { "SourceType", "SALES_ORDER" }, { "SourceDocumentId", "SO-1" },
            { "WarehouseReferenceId", "WH-1" }, { "ShipToReference", "C-1" }, { "PlannedShipAt", DateTimeOffset.UtcNow.ToString("O") }, { "Status", "Dispatched" },
            { "CorrelationId", Guid.NewGuid().ToString() }, { "Version", 3 }, { "CreatedAt", DateTimeOffset.UtcNow.ToString("O") },
            { "Lines", new BsonArray { new BsonDocument { { "LineNumber", "1" }, { "ItemId", Guid.NewGuid().ToString() }, { "SkuId", Guid.NewGuid().ToString() },
                { "Quantity", "1" }, { "UomId", "EA" }, { "InventoryReferenceId", BsonNull.Value } } } }
        };
        await _db.GetCollection<BsonDocument>("sce_shipments").InsertOneAsync(raw);
        var s = (await Repository().GetAsync(_scope, Guid.Parse(raw["_id"].AsString), default))!;
        Assert.Null(s.DispatchAttempt); Assert.Empty(s.DispatchHistory); Assert.Null(s.StockIssued()); Assert.Equal(1, s.NextDispatchAttemptNumber());
        Assert.Null(s.Lines[0].SkuLevel);
        var detail = (await Repository().GetDetailAsync(_scope, s.Id, default))!;
        Assert.Null(detail.Shipment.StockIssued());
    }
}
