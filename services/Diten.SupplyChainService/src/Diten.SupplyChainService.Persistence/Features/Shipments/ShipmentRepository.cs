using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Persistence.Features.Shipments;
public sealed class ShipmentRepository(IMongoDatabase db, IShipmentCommitProbe probe) : IShipmentRepository
{
    private readonly IMongoCollection<Shipment> _shipments = db.GetCollection<Shipment>("sce_shipments");
    private readonly IMongoCollection<BsonDocument> _receipts = db.GetCollection<BsonDocument>("sce_shipment_receipts");
    private static FilterDefinition<Shipment> Scoped(ShipmentScope s)
    {
        if (s.TenantId == Guid.Empty || s.LegalEntityId == Guid.Empty || s.ActorId == Guid.Empty) throw new InvalidOperationException("Trusted scope is required.");
        return Builders<Shipment>.Filter.Where(x => x.TenantId == s.TenantId && x.LegalEntityId == s.LegalEntityId && !x.IsDeleted);
    }
    public async Task<Shipment?> GetAsync(ShipmentScope scope, Guid id, CancellationToken ct) =>
     await _shipments.Find(Scoped(scope) & Builders<Shipment>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);
    public async Task<(IReadOnlyList<Shipment> Items, long Total)> QueryAsync(ShipmentScope scope, ShipmentStatus? status, string? sourceDocumentId, int page, int pageSize, CancellationToken ct)
    {
        var filter = Scoped(scope);
        if (status is not null) filter &= Builders<Shipment>.Filter.Eq(x => x.Status, status.Value);
        if (sourceDocumentId is not null) filter &= Builders<Shipment>.Filter.Eq(x => x.SourceDocumentId, sourceDocumentId);
        var count = await _shipments.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _shipments.Find(filter).SortBy(x => x.Id).Skip(checked((page - 1) * pageSize)).Limit(pageSize).ToListAsync(ct);
        return (items, count);
    }
    public async Task<ShipmentMutationResult> MutateAsync(ShipmentScope scope, Guid? shipmentId, string operation, string key, string fingerprint,
     Guid correlationId, Func<Shipment?, ShipmentChange> decide, CancellationToken ct, Diten.SupplyChainService.Domain.Features.SourceIntake.SourceIntent? source = null)
    {
        var scopeFilter = Scoped(scope);
        var receiptFilter = new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() }, { "Operation", operation }, { "Key", key } };
        for (var attempt = 0; ; attempt++)
        {
            using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
            try
            {
                return await session.WithTransactionAsync(async (transaction, token) =>
                {
                    if (source is not null)
                    {
                        var linkFilter = new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() }, { "Key", source.Key } };
                        var link = await db.GetCollection<BsonDocument>("sce_shipment_source_links").Find(transaction, linkFilter).FirstOrDefaultAsync(token);
                        if (link is not null)
                        {
                            if (link["Hash"].AsString != source.Hash) throw new InvalidOperationException("Source drift must be reconciled before commit.");
                            // The unique link remains reserved even when the aggregate is deleted/cancelled.
                            var existing = await _shipments.Find(transaction, scopeFilter & Builders<Shipment>.Filter.Eq(x => x.Id, source.Shipment.Id)).FirstOrDefaultAsync(token);
                            return new ShipmentMutationResult(existing, true, existing is null ? 404 : 200, existing is null ? "SHIPMENT_NOT_FOUND" : null);
                        }
                    }
                    var receipt = await _receipts.Find(transaction, receiptFilter).FirstOrDefaultAsync(token);
                    if (receipt is not null)
                    {
                        if (receipt["Fingerprint"].AsString != fingerprint) return new ShipmentMutationResult(null, false, 409, "IDEMPOTENCY_KEY_REUSED");
                        if (receipt["CorrelationId"].AsString != correlationId.ToString()) return new ShipmentMutationResult(null, false, 400, "INVALID_REQUEST");
                        var snapshot = JsonSerializer.Deserialize<Shipment>(receipt["Snapshot"].AsString)!;
                        var visible = await _shipments.Find(transaction, scopeFilter & Builders<Shipment>.Filter.Eq(x => x.Id, snapshot.Id)).AnyAsync(token);
                        if (!visible) return new ShipmentMutationResult(null, false, 404, "SHIPMENT_NOT_FOUND");
                        return new ShipmentMutationResult(snapshot, true, operation == "create" ? 200 : receipt["StatusCode"].AsInt32, null);
                    }
                    Shipment? current = shipmentId is null ? null : await _shipments.Find(transaction, scopeFilter & Builders<Shipment>.Filter.Eq(x => x.Id, shipmentId.Value)).FirstOrDefaultAsync(token);
                    if (shipmentId is not null && current is null) return new ShipmentMutationResult(null, false, 404, "SHIPMENT_NOT_FOUND");
                    var oldStatus = current?.Status.ToString(); var oldVersion = current?.Version;
                    var change = decide(current);
                    if (change.ErrorCode is not null) return new ShipmentMutationResult(null, false, change.StatusCode, change.ErrorCode);
                    var s = change.Shipment!;
                    if (s.TenantId != scope.TenantId || s.LegalEntityId != scope.LegalEntityId) throw new InvalidOperationException("Scope mismatch.");
                    if (shipmentId is null) await _shipments.InsertOneAsync(transaction, s, cancellationToken: token);
                    else
                    {
                        s.UpdatedAt = DateTimeOffset.UtcNow;
                        var update = await _shipments.ReplaceOneAsync(transaction, scopeFilter & Builders<Shipment>.Filter.Eq(x => x.Id, s.Id) & Builders<Shipment>.Filter.Eq(x => x.Version, oldVersion!.Value), s, cancellationToken: token);
                        if (update.ModifiedCount != 1) throw new InvalidOperationException("Concurrent shipment mutation.");
                    }
                    var commandId = source?.CommandId ?? Guid.NewGuid();
                    var history = new BsonDocument {{"_id",(source?.HistoryId ?? Guid.NewGuid()).ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
      {"ShipmentId",s.Id.ToString()},{"Sequence",s.Version},{"FromStatus",oldStatus is null?BsonNull.Value:oldStatus},{"ToStatus",s.Status.ToString()},
      {"OccurredAt",change.OccurredAt.UtcDateTime},{"ActorId",scope.ActorId.ToString()},{"CorrelationId",s.CorrelationId.ToString()},{"CommandId",commandId.ToString()},
      {"ReasonCode",change.ReasonCode is null?BsonNull.Value:change.ReasonCode},{"Note",change.Note is null?BsonNull.Value:change.Note}};
                    await db.GetCollection<BsonDocument>("sce_shipment_history").InsertOneAsync(transaction, history, cancellationToken: token);
                    var audit = history.DeepClone().AsBsonDocument; audit["_id"] = (source?.AuditId ?? Guid.NewGuid()).ToString(); audit.Remove("Note"); audit.Remove("ReasonCode"); audit["Operation"] = operation;
                    await db.GetCollection<BsonDocument>("sce_shipment_audit").InsertOneAsync(transaction, audit, cancellationToken: token);
                    var eventTypes = operation == "pod" ? new[] { "ShipmentDelivered", "PodCaptured" } : new[] { operation == "create" ? "ShipmentCreated" : "Shipment" + s.Status };
                    foreach (var eventType in eventTypes)
                    {
                        var eventId = source?.EventId ?? Guid.NewGuid();
                        var payload = JsonSerializer.Serialize(new
                        {
                            eventId,
                            eventType,
                            occurredAt = change.OccurredAt,
                            correlationId = s.CorrelationId,
                            causationId = commandId,
                            aggregateType = "Shipment",
                            aggregateId = s.Id,
                            payload = new { shipmentId = s.Id, shipmentNumber = s.ShipmentNumber, status = s.Status.ToString(), carrierId = s.CarrierId, loadId = s.LoadPlanId, podId = s.Pod?.Id, reasonCode = change.ReasonCode },
                            contractVersion = "v1"
                        });
                        await db.GetCollection<BsonDocument>("sce_shipment_outbox").InsertOneAsync(transaction, new BsonDocument {
       {"_id",eventId.ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},{"ShipmentId",s.Id.ToString()},
       {"EventType",eventType},{"CorrelationId",s.CorrelationId.ToString()},{"CausationId",commandId.ToString()},{"OccurredAt",change.OccurredAt.UtcDateTime},
       {"PayloadJson",payload},{"Status","Pending"},{"AttemptCount",0},{"NextAttemptAt",DateTime.UtcNow},{"ClaimedAt",BsonNull.Value}}, cancellationToken: token);
                    }
                    await _receipts.InsertOneAsync(transaction, new BsonDocument {
      {"_id",(source?.ReceiptId ?? Guid.NewGuid()).ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
      {"Operation",operation},{"Key",key},{"Fingerprint",fingerprint},{"CorrelationId",correlationId.ToString()},{"ShipmentId",s.Id.ToString()},
      {"StatusCode",change.StatusCode},{"Snapshot",JsonSerializer.Serialize(s)},{"CommandId",commandId.ToString()}}, cancellationToken: token);
                    if (source is not null)
                    {
                        var link = new BsonDocument { { "_id", source.Key }, { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() },
                            { "Key", source.Key }, { "SourceModule", "MOD-0178" }, { "SourceType", "WAREHOUSE_OUTBOUND" }, { "SourceDocumentId", s.SourceDocumentId },
                            { "Snapshot", source.Snapshot }, { "Hash", source.Hash }, { "MappingVersion", source.MappingVersion }, { "Defaults", source.Defaults },
                            { "SourceCorrelationId", source.SourceCorrelationId is { } upstream ? upstream.ToString() : BsonNull.Value },
                            { "LocalCorrelationId", source.LocalCorrelationId.ToString() }, { "CorrelationOrigin", source.CorrelationOrigin },
                            { "CorrelationId", s.CorrelationId.ToString() }, { "ShipmentId", s.Id.ToString() }, { "CommandId", commandId.ToString() }, { "State", "Committed" },
                            { "IsDeleted", false }, { "CreatedAt", s.CreatedAt.UtcDateTime }, { "CreatedBy", scope.ActorId.ToString() } };
                        await db.GetCollection<BsonDocument>("sce_shipment_source_links").InsertOneAsync(transaction, link, cancellationToken: token);
                        await db.GetCollection<BsonDocument>("sce_shipment_source_intents").UpdateOneAsync(transaction,
                            new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() }, { "Key", source.Key } },
                            new BsonDocument("$set", new BsonDocument("State", "Committed")), cancellationToken: token);
                    }
                    await probe.BeforeCommitAsync(token);
                    return new ShipmentMutationResult(s, false, change.StatusCode, null);
                }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), ct);
            }
        }
    }
}
