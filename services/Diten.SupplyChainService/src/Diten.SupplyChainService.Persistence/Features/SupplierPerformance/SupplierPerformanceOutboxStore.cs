using System.Text;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;

/// <summary>
/// MOD-0147 §0.3 delivery side of <c>supplier_performance_outbox</c>, the Shipment store's shape: at-least-once, the EventId never
/// changes across retries, a stale Publishing row is claimed again. Registered keyed: a plain IEventOutboxStore would replace Shipment's.
/// </summary>
public sealed class SupplierPerformanceOutboxStore(IMongoDatabase db) : IEventOutboxStore
{
    public const string ServiceKey = "supplier-performance";
    public const string EventName = "supplychain.supplierevaluation.published.v1";
    private IMongoCollection<BsonDocument> Outbox => db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Outbox);
    public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Supplier performance events are inserted inside the submit transaction.");

    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(DateTimeOffset nowUtc, DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default)
    {
        // Trusted background publisher only; this global queue scan is not a user-facing data access path.
        var f = Builders<BsonDocument>.Filter;
        var filter = (f.Eq("Status", "Pending") & f.Lte("NextAttemptAt", nowUtc.UtcDateTime)) | (f.Eq("Status", "Publishing") & f.Lte("ClaimedAt", stalePublishingCutoffUtc.UtcDateTime));
        var doc = await Outbox.FindOneAndUpdateAsync(filter, Builders<BsonDocument>.Update.Set("Status", "Publishing").Set("ClaimedAt", nowUtc.UtcDateTime),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Sort = Builders<BsonDocument>.Sort.Ascending("OccurredAt") }, cancellationToken);
        if (doc is null) return null;
        return new EventOutboxPublishItem(new EventMetadata(Guid.Parse(doc["_id"].AsString), doc["EventName"].AsString, 1, Guid.Parse(doc["CorrelationId"].AsString),
            Guid.Parse(doc["CausationId"].AsString), Guid.Parse(doc["TenantId"].AsString), "Diten.SupplyChainService", new DateTimeOffset(doc["OccurredAt"].ToUniversalTime())),
            Encoding.UTF8.GetBytes(doc["PayloadJson"].AsString), TrustedTransportMetadata.Empty, EventOutboxDeliveryStatus.Publishing, doc["AttemptCount"].AsInt32, null);
    }

    public Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Outbox.UpdateOneAsync(Publishing(eventId), Builders<BsonDocument>.Update.Set("Status", "Published"), cancellationToken: cancellationToken);

    public Task FailPublishAsync(Guid eventId, string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts, CancellationToken cancellationToken = default) =>
        Outbox.UpdateOneAsync(Publishing(eventId), new PipelineUpdateDefinition<BsonDocument>(new[]
        {
            new BsonDocument("$set", new BsonDocument
            {
                { "AttemptCount", new BsonDocument("$add", new BsonArray { "$AttemptCount", 1 }) },
                { "Status", new BsonDocument("$cond", new BsonArray { new BsonDocument("$gte", new BsonArray { new BsonDocument("$add", new BsonArray { "$AttemptCount", 1 }), maxAttempts }), "DeadLettered", "Pending" }) },
                { "LastError", error }, { "NextAttemptAt", nextAttemptAtUtc.UtcDateTime }
            })
        }), cancellationToken: cancellationToken);

    public Task DeadLetterPublishAsync(Guid eventId, EventOutboxTerminalFailure failure, CancellationToken cancellationToken = default) =>
        Outbox.UpdateOneAsync(Publishing(eventId), Builders<BsonDocument>.Update.Set("Status", "DeadLettered").Set("LastError", failure.ReasonCode).Inc("AttemptCount", 1), cancellationToken: cancellationToken);

    private static BsonDocument Publishing(Guid eventId) => new() { { "_id", eventId.ToString() }, { "Status", "Publishing" } };
}
