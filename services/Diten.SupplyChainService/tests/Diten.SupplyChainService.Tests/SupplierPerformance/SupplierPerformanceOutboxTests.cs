using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>DB-010: one fixed database for the class, because the publisher's claim is global; dropped once before it.</summary>
public sealed class OutboxDatabase : IAsyncLifetime
{
    public readonly MongoClient Client = new(SupplierPerformanceMongo.Connection);
    public IMongoDatabase Db => Client.GetDatabase(SupplierPerformanceMongo.OutboxDatabase);
    public async Task InitializeAsync()
    {
        await Client.DropDatabaseAsync(SupplierPerformanceMongo.OutboxDatabase);
        await new SupplierPerformanceSchema(Db).StartAsync(CancellationToken.None);
    }
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// MOD-0147 §0.3 delivery with the real store and the shared publisher behind a test transport. v1 has no production consumer and no
/// transport: T-0147-TXN-02's consumer is a test double, so the production half measured here is "a retry keeps the EventId".
/// </summary>
public sealed class SupplierPerformanceOutboxTests(OutboxDatabase database) : IClassFixture<OutboxDatabase>
{
    private readonly IMongoDatabase _db = database.Db;
    private SupplierPerformanceOutboxStore Store => new(_db);
    private static readonly EventOutboxPublisherOptions Fast = new(20, 3, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(5));

    private sealed class Transport : IEventTransportPublisher
    {
        public int FailuresLeft;
        public readonly List<EventTransportMessage> Sent = [];
        public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            if (FailuresLeft-- > 0) throw new IOException("Broker unavailable.");
            return Task.CompletedTask;
        }
    }

    private async Task<Guid> Pending(string status = "Pending", DateTime? claimedAt = null)
    {
        var id = Guid.NewGuid();
        var row = new BsonDocument
        {
            { "_id", id.ToString() }, { "TenantId", Guid.NewGuid().ToString() }, { "LegalEntityId", Guid.NewGuid().ToString() }, { "EvaluationId", Guid.NewGuid().ToString() }, { "EventName", SupplierPerformanceOutboxStore.EventName },
            { "Status", status }, { "AttemptCount", 0 }, { "OccurredAt", DateTime.UtcNow }, { "NextAttemptAt", DateTime.UtcNow.AddSeconds(-1) }, { "CorrelationId", Guid.NewGuid().ToString() },
            { "CausationId", Guid.NewGuid().ToString() }, { "PayloadJson", """{"eventId":"x"}""" }
        };
        if (claimedAt is { } at) row.Add("ClaimedAt", at);
        await _db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Outbox).InsertOneAsync(row);
        return id;
    }
    private Task<BsonDocument> Row(Guid id) => _db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Outbox).Find(new BsonDocument("_id", id.ToString())).SingleAsync();
    private Task Publish(Transport transport) => new EventOutboxPublisherProcessor(Store, transport, Fast).PublishPendingAsync();

    // T-0147-TXN-02
    [Fact]
    public async Task TXN02_ARetry_ShouldCarryTheSameEventIdSoAConsumerCanIgnoreTheDuplicate()
    {
        var id = await Pending();
        var transport = new Transport { FailuresLeft = 1 };

        await Publish(transport);
        await Publish(transport);

        var sent = transport.Sent.Where(m => m.EventId == id).ToList();
        Assert.Equal(2, sent.Count);
        Assert.Single(sent.Select(m => m.EventId).Distinct()); // the test consumer's whole rule
        Assert.Equal(("Published", 1), ((await Row(id))["Status"].AsString, (await Row(id))["AttemptCount"].AsInt32));
        Assert.Equal((SupplierPerformanceOutboxStore.EventName, """{"eventId":"x"}"""), (sent[0].EventName, Encoding.UTF8.GetString(sent[0].CanonicalPayloadUtf8.Span)));
    }

    // T-0147-TXN-03
    [Fact]
    public async Task TXN03_ACrashBetweenCommitAndPublish_ShouldBeRecoveredFromThePendingRecord()
    {
        var pending = await Pending();
        var transport = new Transport();

        await Publish(transport);

        Assert.Contains(transport.Sent, m => m.EventId == pending);
        Assert.Equal("Published", (await Row(pending))["Status"].AsString);
    }

    [Fact]
    public async Task TXN03_APublisherThatDiedWhilePublishing_ShouldLeaveARowThatIsClaimedAgainWhenStale()
    {
        var stale = await Pending("Publishing", DateTime.UtcNow.AddMinutes(-6));
        var fresh = await Pending("Publishing", DateTime.UtcNow);
        var transport = new Transport();

        await Publish(transport);

        Assert.Contains(transport.Sent, m => m.EventId == stale);
        Assert.DoesNotContain(transport.Sent, m => m.EventId == fresh);
        Assert.Equal("Publishing", (await Row(fresh))["Status"].AsString);
    }

    [Fact]
    public async Task Delivery_WhenAttemptsRunOut_ShouldDeadLetterAndStopRetrying()
    {
        var id = await Pending();
        var transport = new Transport { FailuresLeft = 10 };

        for (var i = 0; i < 4; i++) await Publish(transport);

        Assert.Equal(("DeadLettered", 3), ((await Row(id))["Status"].AsString, (await Row(id))["AttemptCount"].AsInt32));
        Assert.Equal(3, transport.Sent.Count(m => m.EventId == id));
    }

    [Fact]
    public async Task EnqueueOutsideTheSubmitTransaction_ShouldBeRefused()
    {
        var request = new EventOutboxWriteRequest(new EventMetadata(Guid.NewGuid(), SupplierPerformanceOutboxStore.EventName, 1, Guid.NewGuid(), null, Guid.NewGuid(), "x", DateTimeOffset.UtcNow), new byte[] { 1 }, TrustedTransportMetadata.Empty);
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.EnqueueAsync(request));
    }

    // §0.3 + K-1: no transport is composed, so a publication stays Pending and Shipment keeps its own store.
    [Fact]
    public async Task Composition_WithoutATransport_ShouldHoldTheEventPendingAndLeaveShipmentsStoreAlone()
    {
        await using var h = new SupplierPerformanceHost();
        var id = (await h.Create()).Body!["evaluationId"]!.GetValue<string>();

        await h.Submit(id);
        await Task.Delay(1500);

        Assert.Null(h.App.Services.GetService<IEventTransportPublisher>());
        Assert.Equal("Pending", Assert.Single(await h.OutboxRows())["Status"].AsString);
        Assert.IsType<SupplierPerformanceOutboxStore>(h.App.Services.GetRequiredKeyedService<IEventOutboxStore>(SupplierPerformanceOutboxStore.ServiceKey));
        Assert.IsType<ShipmentOutboxStore>(h.App.Services.GetRequiredService<IEventOutboxStore>());
    }
}
