using System.Text;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Persistence.Features.Shipments;
public sealed class ShipmentOutboxStore(IMongoDatabase db, Guid? tenantId = null) : IEventOutboxStore
{
    private readonly IMongoCollection<BsonDocument> _outbox = db.GetCollection<BsonDocument>("sce_shipment_outbox");
    public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request, CancellationToken cancellationToken = default) =>
     throw new NotSupportedException("Shipment events must be inserted inside the shipment transaction.");
    public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(DateTimeOffset nowUtc, DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default)
    {
        // Trusted background publisher only; this global queue scan is not a user-facing data access path.
        var f = Builders<BsonDocument>.Filter;
        var filter = (f.Eq("Status", "Pending") & f.Lte("NextAttemptAt", nowUtc.UtcDateTime)) | (f.Eq("Status", "Publishing") & f.Lte("ClaimedAt", stalePublishingCutoffUtc.UtcDateTime));
        if (tenantId is not null) filter &= f.Eq("TenantId", tenantId.Value.ToString());
        var doc = await _outbox.FindOneAndUpdateAsync(filter, Builders<BsonDocument>.Update.Set("Status", "Publishing").Set("ClaimedAt", nowUtc.UtcDateTime),
         new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Sort = Builders<BsonDocument>.Sort.Ascending("OccurredAt") }, cancellationToken);
        if (doc is null) return null;
        return new EventOutboxPublishItem(new EventMetadata(Guid.Parse(doc["_id"].AsString), doc["EventType"].AsString + ".v1", 1,
         Guid.Parse(doc["CorrelationId"].AsString), Guid.Parse(doc["CausationId"].AsString), Guid.Parse(doc["TenantId"].AsString),
         "Diten.SupplyChainService", new DateTimeOffset(doc["OccurredAt"].ToUniversalTime())), Encoding.UTF8.GetBytes(doc["PayloadJson"].AsString),
         TrustedTransportMetadata.Empty, EventOutboxDeliveryStatus.Publishing, doc["AttemptCount"].AsInt32, null);
    }
    public Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default) =>
     _outbox.UpdateOneAsync(new BsonDocument { { "_id", eventId.ToString() }, { "Status", "Publishing" } }, Builders<BsonDocument>.Update.Set("Status", "Published"), cancellationToken: cancellationToken);
    public Task FailPublishAsync(Guid eventId, string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts, CancellationToken cancellationToken = default) =>
     _outbox.UpdateOneAsync(new BsonDocument { { "_id", eventId.ToString() }, { "Status", "Publishing" } },
      new PipelineUpdateDefinition<BsonDocument>(new[]{new BsonDocument("$set",new BsonDocument {
    {"AttemptCount",new BsonDocument("$add",new BsonArray{"$AttemptCount",1})},
    {"Status",new BsonDocument("$cond",new BsonArray{new BsonDocument("$gte",new BsonArray{new BsonDocument("$add",new BsonArray{"$AttemptCount",1}),maxAttempts}),"DeadLettered","Pending"})},
    {"LastError",error},{"NextAttemptAt",nextAttemptAtUtc.UtcDateTime}})}), cancellationToken: cancellationToken);
    public Task DeadLetterPublishAsync(Guid eventId, EventOutboxTerminalFailure failure, CancellationToken cancellationToken = default) =>
     _outbox.UpdateOneAsync(new BsonDocument { { "_id", eventId.ToString() }, { "Status", "Publishing" } }, Builders<BsonDocument>.Update.Set("Status", "DeadLettered").Set("LastError", failure.ReasonCode).Inc("AttemptCount", 1), cancellationToken: cancellationToken);
}
