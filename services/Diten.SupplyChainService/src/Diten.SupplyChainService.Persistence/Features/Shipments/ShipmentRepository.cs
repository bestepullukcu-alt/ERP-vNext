using Diten.BuildingBlocks.CentralAudit;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Persistence.Features.Shipments;
public sealed class ShipmentRepository(IMongoDatabase db, IShipmentCommitProbe probe) : IShipmentRepository
{
    private readonly IMongoCollection<Shipment> _shipments = db.GetCollection<Shipment>("sce_shipments");
    private readonly IMongoCollection<BsonDocument> _shipmentDocuments = db.GetCollection<BsonDocument>("sce_shipments");
    private readonly IMongoCollection<BsonDocument> _receipts = db.GetCollection<BsonDocument>("sce_shipment_receipts");
    private static FilterDefinition<Shipment> Scoped(ShipmentScope s)
    {
        if (s.TenantId == Guid.Empty || s.LegalEntityId == Guid.Empty || s.ActorId == Guid.Empty) throw new InvalidOperationException("Trusted scope is required.");
        return Builders<Shipment>.Filter.Where(x => x.TenantId == s.TenantId && x.LegalEntityId == s.LegalEntityId && !x.IsDeleted);
    }
    public async Task<Shipment?> GetAsync(ShipmentScope scope, Guid id, CancellationToken ct) =>
     await _shipments.Find(Scoped(scope) & Builders<Shipment>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);
    public async Task<ShipmentDetailReadResult?> GetDetailAsync(ShipmentScope scope, Guid id, CancellationToken ct)
    {
        if (scope.TenantId == Guid.Empty || scope.LegalEntityId == Guid.Empty || scope.ActorId == Guid.Empty)
            throw new InvalidOperationException("Trusted scope is required.");
        var filter = new BsonDocument {
            { "_id", id.ToString() }, { "TenantId", scope.TenantId.ToString() },
            { "LegalEntityId", scope.LegalEntityId.ToString() }, { "IsDeleted", false }
        };
        var raw = await _shipmentDocuments.Find(filter).FirstOrDefaultAsync(ct);
        return raw is null ? null : ShipmentDetailMaterializer.Materialize(raw);
    }
    public async Task<(IReadOnlyList<Shipment> Items, long Total)> QueryAsync(ShipmentScope scope, ShipmentStatus? status, string? sourceDocumentId, int page, int pageSize, CancellationToken ct)
    {
        var filter = Scoped(scope);
        if (status is not null) filter &= Builders<Shipment>.Filter.Eq(x => x.Status, status.Value);
        if (sourceDocumentId is not null) filter &= Builders<Shipment>.Filter.Eq(x => x.SourceDocumentId, sourceDocumentId);
        var count = await _shipments.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _shipments.Find(filter).SortBy(x => x.Id).Skip(checked((page - 1) * pageSize)).Limit(pageSize).ToListAsync(ct);
        return (items, count);
    }
    private static FilterDefinition<BsonDocument> ReceiptFilter(ShipmentScope scope, string operation, string key) =>
        new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() }, { "Operation", operation }, { "Key", key } };
    private async Task<ShipmentMutationResult> ReplayAsync(IClientSessionHandle? session, FilterDefinition<Shipment> scopeFilter, BsonDocument receipt,
     string operation, string fingerprint, Guid correlationId, CancellationToken ct)
    {
        if (receipt["Fingerprint"].AsString != fingerprint) return new ShipmentMutationResult(null, false, 409, "IDEMPOTENCY_KEY_REUSED");
        if (receipt["CorrelationId"].AsString != correlationId.ToString()) return new ShipmentMutationResult(null, false, 400, "INVALID_REQUEST");
        var shipmentFilter = scopeFilter & Builders<Shipment>.Filter.Eq(x => x.Id, Guid.Parse(receipt["ShipmentId"].AsString));
        var visible = session is null ? await _shipments.Find(shipmentFilter).AnyAsync(ct) : await _shipments.Find(session, shipmentFilter).AnyAsync(ct);
        if (!visible) return new ShipmentMutationResult(null, false, 404, "SHIPMENT_NOT_FOUND");
        // MOD-0183 §23.4: a compensated dispatch stores its refusal as the receipt, so the same command answers the same.
        if (receipt.TryGetValue("ErrorCode", out var error) && error.IsString)
            return new ShipmentMutationResult(null, true, receipt["StatusCode"].AsInt32, error.AsString,
                receipt.TryGetValue("DetailsJson", out var details) && details.IsString ? details.AsString : null);
        var snapshot = JsonSerializer.Deserialize<Shipment>(receipt["Snapshot"].AsString)!;
        return new ShipmentMutationResult(snapshot, true, operation == "create" ? 200 : receipt["StatusCode"].AsInt32, null);
    }
    public async Task<ShipmentMutationResult?> FindReceiptAsync(ShipmentScope scope, string operation, string key, string fingerprint, Guid correlationId, CancellationToken ct)
    {
        var receipt = await _receipts.Find(ReceiptFilter(scope, operation, key)).FirstOrDefaultAsync(ct);
        if (receipt is null) return null;
        var result = await ReplayAsync(null, Scoped(scope), receipt, operation, fingerprint, correlationId, ct);
        if (result.Replay && result.ErrorCode is null) ShipmentTelemetry.IdempotentReplays.Add(1);
        return result;
    }
    private FilterDefinition<Shipment> Held(ShipmentScope scope, Guid shipmentId, int n, Guid leaseId) =>
        Scoped(scope) & Builders<Shipment>.Filter.Eq(x => x.Id, shipmentId)
        & Builders<Shipment>.Filter.Eq(x => x.DispatchAttempt!.N, n) & Builders<Shipment>.Filter.Eq(x => x.DispatchAttempt!.LeaseId, leaseId);
    public async Task<bool> TryBeginOrResumeDispatchAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt? observed, DispatchAttempt next, CancellationToken ct)
    {
        var f = Builders<Shipment>.Filter;
        var filter = Scoped(scope) & f.Eq(x => x.Id, shipmentId) & f.Eq(x => x.Status, ShipmentStatus.Planned) & (observed is null
            ? f.Eq(x => x.DispatchAttempt, null)
            : f.Eq(x => x.DispatchAttempt!.N, observed.N) & f.Eq(x => x.DispatchAttempt!.LeaseId, observed.LeaseId) & f.Eq(x => x.DispatchAttempt!.State, observed.State));
        var update = await _shipments.UpdateOneAsync(filter, Builders<Shipment>.Update.Set(x => x.DispatchAttempt, next), cancellationToken: ct);
        return update.ModifiedCount == 1;
    }
    public async Task<bool> SaveDispatchAttemptAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt attempt, Guid heldLeaseId, CancellationToken ct) =>
        (await _shipments.UpdateOneAsync(Held(scope, shipmentId, attempt.N, heldLeaseId), Builders<Shipment>.Update.Set(x => x.DispatchAttempt, attempt), cancellationToken: ct)).ModifiedCount == 1;
    public async Task<bool> CloseCompensatedAttemptAsync(ShipmentScope scope, Guid shipmentId, DispatchAttempt attempt, Guid heldLeaseId, CancellationToken ct)
    {
        var closed = attempt.Copy(); closed.State = DispatchState.Compensated;
        var update = Builders<Shipment>.Update.Set(x => x.DispatchAttempt, null).Push(x => x.DispatchHistory, closed);
        return (await _shipments.UpdateOneAsync(Held(scope, shipmentId, attempt.N, heldLeaseId), update, cancellationToken: ct)).ModifiedCount == 1;
    }
    public async Task<bool> DropUnpostedAttemptAsync(ShipmentScope scope, Guid shipmentId, int n, Guid heldLeaseId, CancellationToken ct) =>
        (await _shipments.UpdateOneAsync(Held(scope, shipmentId, n, heldLeaseId) & Builders<Shipment>.Filter.Eq(x => x.DispatchAttempt!.State, DispatchState.InProgress),
            Builders<Shipment>.Update.Set(x => x.DispatchAttempt, null), cancellationToken: ct)).ModifiedCount == 1;
    public async Task AppendLateWriteAsync(ShipmentScope scope, Guid shipmentId, DispatchLateWrite write, CancellationToken ct) =>
        await _shipments.UpdateOneAsync(Scoped(scope) & Builders<Shipment>.Filter.Eq(x => x.Id, shipmentId),
            Builders<Shipment>.Update.Push(x => x.DispatchLateWrites, write), cancellationToken: ct);
    public async Task StoreOutcomeReceiptAsync(ShipmentScope scope, Guid shipmentId, string operation, string key, string fingerprint, Guid correlationId,
     int statusCode, string errorCode, string? detailsJson, CancellationToken ct)
    {
        try
        {
            await _receipts.InsertOneAsync(new BsonDocument {
                {"_id",Guid.NewGuid().ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"Operation",operation},{"Key",key},{"Fingerprint",fingerprint},{"CorrelationId",correlationId.ToString()},{"ShipmentId",shipmentId.ToString()},
                {"StatusCode",statusCode},{"ErrorCode",errorCode},{"DetailsJson",detailsJson is null ? BsonNull.Value : detailsJson},{"CommandId",Guid.NewGuid().ToString()}}, cancellationToken: ct);
        }
        // The unique replay index already holds this key: an identical command stored the same outcome first.
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
    }
    public async Task AppendDispatchAuditAsync(ShipmentScope scope, Guid shipmentId, DispatchAuditEntry entry, CancellationToken ct) =>
        await db.GetCollection<BsonDocument>("sce_shipment_audit").InsertOneAsync(new BsonDocument {
            {"_id",Guid.NewGuid().ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
            {"ShipmentId",shipmentId.ToString()},{"Operation","dispatch-attempt"},{"Attempt",entry.N},{"Outcome",entry.Outcome},
            {"OccurredAt",entry.OccurredAt.UtcDateTime},{"ActorId",scope.ActorId.ToString()},{"CorrelationId",entry.CorrelationId.ToString()},
            {"IssueTransactionIds",new BsonArray(entry.IssueTransactionIds)},{"ReversalTransactionIds",new BsonArray(entry.ReversalTransactionIds)},
            {"IntegrityFaults",new BsonArray(entry.IntegrityFaults)}}, cancellationToken: ct);
    public async Task<ShipmentMutationResult> MutateAsync(ShipmentScope scope, Guid? shipmentId, string operation, string key, string fingerprint,
     Guid correlationId, Func<Shipment?, ShipmentChange> decide, CancellationToken ct, Diten.SupplyChainService.Domain.Features.SourceIntake.SourceIntent? source = null)
    {
        var scopeFilter = Scoped(scope);
        var receiptFilter = ReceiptFilter(scope, operation, key);
        for (var attempt = 0; ; attempt++)
        {
            using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
            try
            {
                var enqueued = 0;
                var result = await session.WithTransactionAsync(async (transaction, token) =>
                {
                    // The driver may run this body more than once; only the committed run's count survives.
                    enqueued = 0;
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
                    if (receipt is not null) return await ReplayAsync(transaction, scopeFilter, receipt, operation, fingerprint, correlationId, token);
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
                    await CentralAuditTransaction.EnlistAsync(transaction, s.Id, token);
                    await probe.BeforeCommitAsync(token);
                    enqueued = eventTypes.Length;
                    return new ShipmentMutationResult(s, false, change.StatusCode, null);
                }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
                // O-3 replay and O-4 pending are counted once, after commit and outside every retry, so a retry is not a second event.
                if (result.Replay && result.ErrorCode is null) ShipmentTelemetry.IdempotentReplays.Add(1);
                if (enqueued > 0) ShipmentTelemetry.OutboxPending.Add(enqueued);
                return result;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), ct);
            }
        }
    }
}
