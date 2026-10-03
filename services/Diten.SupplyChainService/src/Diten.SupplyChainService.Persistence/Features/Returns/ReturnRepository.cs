using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Persistence.Features.Returns;

public sealed class ReturnRepository(IMongoDatabase database, IReturnCommitProbe probe) : IReturnRepository
{
    private IMongoCollection<ReturnOrder> Orders => database.GetCollection<ReturnOrder>("returns");
    private IMongoCollection<ReturnEntitlement> Entitlements => database.GetCollection<ReturnEntitlement>("return_entitlements");
    private IMongoCollection<BsonDocument> Receipts => database.GetCollection<BsonDocument>("returns_receipts").WithReadConcern(ReadConcern.Majority);
    private static BsonDocument Scope(ReturnScope scope)
    {
        scope.EnsureTrusted();
        return new() { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() } };
    }
    private static BsonDocument Visible(ReturnScope scope)
    { var filter = Scope(scope); filter.Add("IsDeleted", false); return filter; }
    public async Task<IReadOnlyList<ReturnOrder>> QueryAsync(ReturnScope scope, ReturnStatus? status, Guid? shipment, CancellationToken ct)
    {
        var filter = Visible(scope);
        if (status is not null) filter.Add("Status", status.ToString());
        if (shipment is not null) filter.Add("ShipmentId", shipment.ToString());
        try { return await Orders.Find(filter).ToListAsync(ct); }
        catch (Exception ex) when (ex is MongoException or TimeoutException or MongoDB.Bson.BsonSerializationException)
        { throw new ReturnFailureException(503, "PERSISTENCE_UNAVAILABLE"); }
    }
    private static ReturnMutationResult Replay(BsonDocument receipt, string fingerprint, Guid root)
    {
        if (receipt["CorrelationRoot"].AsString != root.ToString()) return ReturnMutationResult.Error(409, "CORRELATION_ROOT_MISMATCH");
        if (receipt["Fingerprint"].AsString != fingerprint) return ReturnMutationResult.Error(409, "IDEMPOTENCY_KEY_REUSED");
        return new(Guid.Parse(receipt["ReturnId"].AsString), receipt["RmaNumber"].AsString,
            Guid.Parse(receipt["ShipmentId"].AsString), receipt["ResultingStatus"].AsString, true, receipt["StatusCode"].AsInt32);
    }
    public async Task<ReturnMutationResult> MutateAsync(ReturnScope scope, Guid? id, string key, string fingerprint,
        Guid root, ReturnOrder? create, ReturnStatus? target, string? occurredAt, string? inventoryReference,
        string? dispositionCode, Func<ReturnOrder, CancellationToken, Task<ReturnReferenceSnapshot>> observe, CancellationToken ct, string? rawCreateJson = null)
    {
        var identity = Scope(scope);
        identity.Add("Operation", id is null ? "createReturn" : "transitionReturn");
        identity.Add("TargetId", id?.ToString() ?? "create"); identity.Add("IdempotencyKey", key);
        ReturnReferenceSnapshot? observation = null;
        try
        {
            // Remote observation is outside the local transaction. A committed replay never re-reads Shipment.
            var prior = await Receipts.Find(identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
            if (prior is not null) return Replay(prior, fingerprint, root);
            if (id is null)
            {
                if (create!.Lines.Select(x => x.ShipmentLineNumber).Distinct(StringComparer.Ordinal).Count() != create.Lines.Count)
                    throw new ReturnFailureException(422, "DUPLICATE_RETURN_LINE");
                if (create.Lines.Any(x => !ReturnQuantity.TryParse(x.Quantity, out var q) || q.CompareTo(ReturnQuantity.Zero) <= 0))
                    throw new ReturnFailureException(422, "INVALID_RETURN_QUANTITY");
                observation = await observe(create, ct);
                if (observation.ShipmentId != create.ShipmentId) throw new ReturnFailureException(502, "DEPENDENCY_RESPONSE_INVALID");
                if (observation.Root != root) throw new ReturnFailureException(409, "CORRELATION_ROOT_MISMATCH");
                if (observation.Status is not ("Delivered" or "Closed")) throw new ReturnFailureException(422, "SHIPMENT_NOT_RETURNABLE");
            }
        }
        catch (ReturnFailureException ex) { return ReturnMutationResult.Error(ex.Status, ex.Code); }
        catch (Exception ex) when (ex is MongoException or TimeoutException or MongoDB.Bson.BsonSerializationException) { return ReturnMutationResult.Error(503, "PERSISTENCE_UNAVAILABLE"); }
        var numberAttempts = 0;
        Guid? allocatedId = null;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var commitAttempted = false;
            try
            {
                using var session = await database.Client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary,
                    WriteConcern.WMajority, maxCommitTime: TimeSpan.FromSeconds(5)));
                try
                {
                    var receipt = await Receipts.Find(session, identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
                    if (receipt is not null) { await session.AbortTransactionAsync(ct); return Replay(receipt, fingerprint, root); }
                    ReturnOrder order;
                    string? previousStatus = null; Guid? cause = null;
                    if (id is null)
                    {
                        order = JsonSerializer.Deserialize<ReturnOrder>(JsonSerializer.Serialize(create))!;
                        allocatedId ??= probe.NewId();
                        order.Id = allocatedId.Value; order.RmaNumber = "RMA-" + order.Id.ToString("N").ToUpperInvariant();
                        order.TenantId = scope.TenantId; order.LegalEntityId = scope.LegalEntityId; order.CreatedBy = scope.ActorId;
                        order.CreatedAt = DateTimeOffset.UtcNow; order.Status = ReturnStatus.Requested; order.Version = 1;
                        order.CorrelationRoot = observation!.Root;
                        await Debit(session, scope, order, observation, ct);
                    }
                    else
                    {
                        var visible = Visible(scope); visible.Add("_id", id.Value.ToString());
                        order = await Orders.Find(session, visible).FirstOrDefaultAsync(ct) ?? throw new ReturnFailureException(404, "RETURN_NOT_FOUND");
                        if (order.CorrelationRoot != root) throw new ReturnFailureException(409, "CORRELATION_ROOT_MISMATCH");
                        if (!ReturnLifecycle.Allows(order.Status, target!.Value)) throw new ReturnFailureException(422, "INVALID_RETURN_TRANSITION");
                        if (target == ReturnStatus.Dispositioned && string.IsNullOrEmpty(dispositionCode)) throw new ReturnFailureException(422, "DISPOSITION_REQUIRED");
                        previousStatus = order.Status.ToString(); cause = order.LastEventId;
                        if (ReturnLifecycle.Releases(target.Value)) await Release(session, scope, order, ct);
                        order.Status = target.Value; order.UpdatedBy = scope.ActorId; order.UpdatedAt = DateTimeOffset.UtcNow;
                        order.Version++; order.InventoryTransactionReferenceId = inventoryReference;
                        if (target == ReturnStatus.Dispositioned) order.DispositionCode = dispositionCode;
                    }
                    var eventId = Guid.NewGuid(); order.LastEventId = eventId;
                    if (id is null) await Orders.InsertOneAsync(session, order, cancellationToken: ct);
                    else
                    {
                        var cas = Visible(scope); cas.Add("_id", order.Id.ToString()); cas.Add("Version", order.Version - 1);
                        if ((await Orders.ReplaceOneAsync(session, cas, order, cancellationToken: ct)).ModifiedCount != 1) throw new RetryReturnTransactionException();
                    }
                    await probe.AtAsync("aggregate", ct);
                    var saved = identity.DeepClone().AsBsonDocument;
                    saved.Add("_id", Guid.NewGuid().ToString()); saved.Add("CorrelationRoot", root.ToString()); saved.Add("Fingerprint", fingerprint);
                    saved.Add("ReturnId", order.Id.ToString()); saved.Add("RmaNumber", order.RmaNumber); saved.Add("ShipmentId", order.ShipmentId.ToString());
                    saved.Add("ResultingStatus", order.Status.ToString()); saved.Add("StatusCode", id is null ? 201 : 200); saved.Add("ActorId", scope.ActorId.ToString());
                    await Receipts.InsertOneAsync(session, saved, cancellationToken: ct); await probe.AtAsync("receipt", ct);
                    var audit = Scope(scope); audit.Add("_id", Guid.NewGuid().ToString()); audit.Add("ReturnId", order.Id.ToString());
                    audit.Add("ActorId", scope.ActorId.ToString()); audit.Add("Version", order.Version); audit.Add("CorrelationRoot", root.ToString());
                    audit.Add("FromStatus", Text(previousStatus)); audit.Add("ToStatus", order.Status.ToString());
                    audit.Add("OccurredAt", Text(occurredAt)); audit.Add("CommittedAt", DateTimeOffset.UtcNow.ToString("O"));
                    audit.Add("InventoryTransactionReferenceId", Text(inventoryReference)); audit.Add("DispositionCode", Text(dispositionCode));
                    audit.Add("EvidenceType", target == ReturnStatus.Received ? "manual-assertion" : BsonNull.Value);
                    audit.Add("RawCreate", create is null ? BsonNull.Value : BsonDocument.Parse(rawCreateJson ?? JsonSerializer.Serialize(create)));
                    audit.Add("ReferenceObservation", observation is null ? BsonNull.Value : BsonDocument.Parse(JsonSerializer.Serialize(observation)));
                    await database.GetCollection<BsonDocument>("returns_audit").InsertOneAsync(session, audit, cancellationToken: ct);
                    await probe.AtAsync("audit", ct);
                    var envelope = new
                    {
                        eventId, eventType = "Return" + order.Status,
                        occurredAt = id is null ? order.CreatedAt.ToUniversalTime().ToString("O") : ReturnInstant.Normalize(occurredAt!),
                        correlationId = order.CorrelationRoot, causationId = cause, aggregateType = "Return", aggregateId = order.Id,
                        payload = new { returnId = order.Id, rmaNumber = order.RmaNumber, shipmentId = order.ShipmentId,
                            status = order.Status.ToString(), inventoryTransactionReferenceId = order.InventoryTransactionReferenceId }, contractVersion = "v1"
                    };
                    var pending = Scope(scope); pending.Add("_id", Guid.NewGuid().ToString()); pending.Add("EventId", eventId.ToString());
                    pending.Add("ReturnId", order.Id.ToString()); pending.Add("Status", "Pending"); pending.Add("Envelope", BsonDocument.Parse(JsonSerializer.Serialize(envelope)));
                    await database.GetCollection<BsonDocument>("returns_outbox").InsertOneAsync(session, pending, cancellationToken: ct);
                    await probe.AtAsync("outbox", ct); await probe.AtAsync("beforeCommit", ct);
                    commitAttempted = true;
                    for (var commit = 0; ; commit++)
                    {
                        try { await session.CommitTransactionAsync(ct); break; }
                        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }
                    }
                    await probe.AtAsync("afterCommit", ct);
                    return new(order.Id, order.RmaNumber, order.ShipmentId, order.Status.ToString(), false, id is null ? 201 : 200);
                }
                catch
                {
                    if (!commitAttempted && session.IsInTransaction)
                        try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { }
                    throw;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ReturnFailureException ex) { return ReturnMutationResult.Error(ex.Status, ex.Code); }
            catch (RetryReturnTransactionException) { await Task.Delay(5 * (attempt + 1), ct); }
            catch (MongoException ex) when (!ex.HasErrorLabel("UnknownTransactionCommitResult") &&
                (ex.HasErrorLabel("TransientTransactionError") || (!commitAttempted && ex is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey })))
            {
                if (ex is MongoWriteException write && (write.Message.Contains("return_number") || write.Message.Contains("returns index: _id_")))
                {
                    if (++numberAttempts >= 3) return ReturnMutationResult.Error(503, "PERSISTENCE_UNAVAILABLE");
                    allocatedId = null;
                }
                await Task.Delay(5 * (attempt + 1), ct);
            }
            catch (Exception ex) when (ex is MongoException or TimeoutException or MongoDB.Bson.BsonSerializationException)
            {
                if (commitAttempted)
                {
                    try
                    {
                        var recovered = await Receipts.Find(identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
                        if (recovered is not null) return Replay(recovered, fingerprint, root);
                    }
                    catch (Exception recovery) when (recovery is MongoException or TimeoutException) { }
                }
                return ReturnMutationResult.Error(503, "PERSISTENCE_UNAVAILABLE");
            }
            catch (Exception) { return ReturnMutationResult.Error(500, "INTERNAL_ERROR"); }
        }
        return ReturnMutationResult.Error(503, "PERSISTENCE_UNAVAILABLE");
    }
    private static BsonValue Text(string? value) => value is null ? BsonNull.Value : new BsonString(value);
    private static BsonDocument EntitlementKey(ReturnScope scope, Guid shipment, string line)
    { var filter = Scope(scope); filter.Add("ShipmentId", shipment.ToString()); filter.Add("LineNumber", line); return filter; }
    private async Task Debit(IClientSessionHandle session, ReturnScope scope, ReturnOrder order, ReturnReferenceSnapshot snapshot, CancellationToken ct)
    {
        foreach (var line in order.Lines)
        {
            var source = snapshot.Lines.SingleOrDefault(x => x.LineNumber == line.ShipmentLineNumber)
                ?? throw new ReturnFailureException(404, "SHIPMENT_LINE_NOT_FOUND");
            var key = EntitlementKey(scope, order.ShipmentId, line.ShipmentLineNumber);
            var stored = await Entitlements.Find(session, key, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
            var cap = ReturnQuantity.Parse(source.Quantity);
            if (stored is not null && (!ReturnQuantity.Parse(stored.SourceQuantity).Equals(cap) || stored.UomId != source.UomId || stored.ItemId != source.ItemId || stored.SkuId != source.SkuId))
                throw new ReturnFailureException(409, "RETURN_SOURCE_CHANGED");
            if (line.UomId != source.UomId) throw new ReturnFailureException(422, "RETURN_UOM_MISMATCH");
            var next = ReturnQuantity.Parse(stored?.UsedQuantity ?? "0") + ReturnQuantity.Parse(line.Quantity);
            if (next.CompareTo(cap) > 0) throw new ReturnFailureException(422, "RETURN_QUANTITY_EXCEEDED");
            if (stored is null)
                await Entitlements.InsertOneAsync(session, new ReturnEntitlement
                {
                    Id = Guid.NewGuid(), TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, ShipmentId = order.ShipmentId,
                    LineNumber = source.LineNumber, SourceQuantity = source.Quantity, UomId = source.UomId,
                    ItemId = source.ItemId, SkuId = source.SkuId, UsedQuantity = next.ToString(), Version = 1
                }, cancellationToken: ct);
            else
            {
                key.Add("Version", stored.Version); stored.Version++; stored.UsedQuantity = next.ToString();
                if ((await Entitlements.ReplaceOneAsync(session, key, stored, new ReplaceOptions { Collation = Collation.Simple }, ct)).ModifiedCount != 1) throw new RetryReturnTransactionException();
            }
            await probe.AtAsync("entitlement", ct);
        }
    }
    private async Task Release(IClientSessionHandle session, ReturnScope scope, ReturnOrder order, CancellationToken ct)
    {
        foreach (var line in order.Lines)
        {
            var key = EntitlementKey(scope, order.ShipmentId, line.ShipmentLineNumber);
            var stored = await Entitlements.Find(session, key, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct) ?? throw new ReturnFailureException(503, "PERSISTENCE_UNAVAILABLE");
            var next = ReturnQuantity.Parse(stored.UsedQuantity) - ReturnQuantity.Parse(line.Quantity);
            if (next.CompareTo(ReturnQuantity.Zero) < 0) throw new ReturnFailureException(503, "PERSISTENCE_UNAVAILABLE");
            key.Add("Version", stored.Version); stored.Version++; stored.UsedQuantity = next.ToString();
            if ((await Entitlements.ReplaceOneAsync(session, key, stored, new ReplaceOptions { Collation = Collation.Simple }, ct)).ModifiedCount != 1) throw new RetryReturnTransactionException();
            await probe.AtAsync("entitlement", ct);
        }
    }
    private sealed class RetryReturnTransactionException : Exception { }
}
