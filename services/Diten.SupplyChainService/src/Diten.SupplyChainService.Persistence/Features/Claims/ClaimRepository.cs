using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Claims;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.SupplyChainService.Persistence.Features.Claims;

public sealed class ClaimRepository(IMongoDatabase database, IClaimCommitProbe probe) : IClaimRepository
{
    private IMongoCollection<Claim> Claims => database.GetCollection<Claim>("claims");
    private IMongoCollection<BsonDocument> Receipts => database.GetCollection<BsonDocument>("claims_receipts").WithReadConcern(ReadConcern.Majority);
    private static BsonDocument Scope(ClaimScope scope)
    {
        scope.EnsureTrusted();
        return new() { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() } };
    }
    private static BsonDocument Visible(ClaimScope scope)
    {
        var filter = Scope(scope); filter.Add("IsDeleted", false); return filter;
    }
    public async Task<IReadOnlyList<Claim>> QueryAsync(ClaimScope scope, ClaimStatus? status, Guid? shipment, CancellationToken ct)
    {
        var filter = Visible(scope);
        if (status.HasValue) filter.Add("Status", status.Value.ToString());
        if (shipment.HasValue) filter.Add("ShipmentId", shipment.Value.ToString());
        try { return await Claims.Find(filter).ToListAsync(ct); }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { throw new ClaimFailureException(503, "CLAIM_STORAGE_UNAVAILABLE"); }
    }
    private static ClaimMutationResult Replay(BsonDocument receipt, string fingerprint, Guid root)
    {
        if (Guid.Parse(receipt["CorrelationRoot"].AsString) != root) return ClaimMutationResult.Error(409, "CLAIM_CORRELATION_MISMATCH");
        if (receipt["Fingerprint"].AsString != fingerprint) return ClaimMutationResult.Error(409, "IDEMPOTENCY_KEY_REUSED");
        return new(Guid.Parse(receipt["ClaimId"].AsString), receipt["ClaimNumber"].AsString, Guid.Parse(receipt["ShipmentId"].AsString),
            receipt["ResultingStatus"].AsString, receipt["ApprovedAmount"].IsBsonNull ? null : receipt["ApprovedAmount"].AsString, true, receipt["StatusCode"].AsInt32);
    }
    private async Task RequireIndexes(CancellationToken ct)
    {
        foreach (var spec in new[] { ("claims", "claim_number", new[] { "ClaimNumber" }), ("claims_receipts", "claim_receipt", new[] { "Operation", "TargetId", "IdempotencyKey" }), ("claims_outbox", "claim_event", new[] { "EventId" }) })
        {
            using var cursor = await database.GetCollection<BsonDocument>(spec.Item1).Indexes.ListAsync(ct);
            var indexes = await cursor.ToListAsync(ct);
            var index = indexes.SingleOrDefault(x => x.GetValue("name", "").AsString == spec.Item2);
            var keys = new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } };
            foreach (var key in spec.Item3) keys.Add(key, 1);
            if (index is null || !index.GetValue("unique", false).AsBoolean || !index["key"].AsBsonDocument.Equals(keys) ||
                index.Contains("partialFilterExpression") || index.Contains("expireAfterSeconds") || index.GetValue("sparse", false).AsBoolean ||
                (index.Contains("collation") && index["collation"].AsBsonDocument.GetValue("locale", "simple") != "simple"))
                throw new ClaimFailureException(503, "CLAIM_STORAGE_UNAVAILABLE");
        }
    }
    public async Task<ClaimMutationResult> MutateAsync(ClaimScope scope, Guid? id, string key, string fingerprint, Guid root,
        Claim? create, ClaimStatus? target, string? occurredAt, string? approvedAmount, string? resolutionCode, string? note,
        Func<Claim, CancellationToken, Task<ClaimReferenceSnapshot>> observe, CancellationToken ct)
    {
        var identity = Scope(scope);
        identity.Add("Operation", id.HasValue ? "transitionClaim" : "createClaim");
        identity.Add("TargetId", id?.ToString() ?? "create"); identity.Add("IdempotencyKey", key);
        var receivedAt = DateTimeOffset.UtcNow;
        Claim? prepared = null;
        try
        {
            // Receipt identity excludes actor/root; root is checked before fingerprint.
            var original = await Receipts.Find(identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
            if (original is not null) return Replay(original, fingerprint, root);
            await RequireIndexes(ct);
            if (!id.HasValue)
            {
                prepared = JsonSerializer.Deserialize<Claim>(JsonSerializer.Serialize(create)) ?? throw new ClaimFailureException(400, "INVALID_REQUEST");
                var snapshot = await observe(prepared, ct); // HTTP observation is outside the Mongo transaction.
                if (snapshot.Root != root) return ClaimMutationResult.Error(409, "CLAIM_CORRELATION_MISMATCH");
                ClaimLifecycle.ValidateCreate(prepared, snapshot);
                prepared.Id = probe.NewId(); prepared.ClaimNumber = "CLM-" + prepared.Id.ToString("N");
                prepared.TenantId = scope.TenantId; prepared.LegalEntityId = scope.LegalEntityId;
                prepared.CreatedBy = scope.ActorId; prepared.CreatedAt = receivedAt; prepared.Version = 1;
                prepared.Status = ClaimStatus.Open; prepared.CorrelationRoot = root; prepared.ReferenceSnapshot = snapshot;
            }
        }
        catch (ClaimFailureException ex) { return ClaimMutationResult.Error(ex.Status, ex.Code); }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE"); }

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var commitAttempted = false;
            try
            {
                using var session = await database.Client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority, maxCommitTime: TimeSpan.FromSeconds(5)));
                try
                {
                    var receipt = await Receipts.Find(session, identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
                    if (receipt is not null)
                    {
                        await session.AbortTransactionAsync(ct); return Replay(receipt, fingerprint, root);
                    }
                    Claim claim; BsonValue before = BsonNull.Value;
                    if (!id.HasValue) claim = JsonSerializer.Deserialize<Claim>(JsonSerializer.Serialize(prepared))!;
                    else
                    {
                        var filter = Visible(scope); filter.Add("_id", id.Value.ToString());
                        claim = await Claims.Find(session, filter).FirstOrDefaultAsync(ct) ?? throw new ClaimFailureException(404, "CLAIM_NOT_FOUND");
                        if (claim.CorrelationRoot != root) throw new ClaimFailureException(409, "CLAIM_CORRELATION_MISMATCH");
                        ClaimLifecycle.ValidateTransition(claim, target!.Value, approvedAmount);
                        before = claim.ToBsonDocument();
                        claim.Status = target.Value;
                        if (target == ClaimStatus.Approved) claim.ApprovedAmount = approvedAmount;
                        claim.UpdatedAt = DateTimeOffset.UtcNow; claim.UpdatedBy = scope.ActorId; claim.Version++;
                    }
                    var eventId = Guid.NewGuid(); claim.LastEventId = eventId;
                    if (!id.HasValue) await Claims.InsertOneAsync(session, claim, cancellationToken: ct);
                    else
                    {
                        var filter = Visible(scope); filter.Add("_id", claim.Id.ToString()); filter.Add("Version", claim.Version - 1);
                        var changed = await Claims.ReplaceOneAsync(session, filter, claim, cancellationToken: ct);
                        if (changed.ModifiedCount != 1) throw new ClaimCasConflictException();
                    }
                    await probe.AtAsync("aggregate", ct);
                    var saved = identity.DeepClone().AsBsonDocument;
                    saved.Add("_id", Guid.NewGuid().ToString()); saved.Add("Fingerprint", fingerprint); saved.Add("CorrelationRoot", root.ToString());
                    saved.Add("ClaimId", claim.Id.ToString()); saved.Add("ClaimNumber", claim.ClaimNumber); saved.Add("ShipmentId", claim.ShipmentId.ToString());
                    saved.Add("ResultingStatus", claim.Status.ToString()); saved.Add("ApprovedAmount", Text(claim.ApprovedAmount));
                    saved.Add("ActorId", scope.ActorId.ToString()); saved.Add("StatusCode", id.HasValue ? 200 : 201);
                    await Receipts.InsertOneAsync(session, saved, cancellationToken: ct); await probe.AtAsync("receipt", ct);
                    var audit = Scope(scope);
                    audit.Add("_id", Guid.NewGuid().ToString()); audit.Add("ClaimId", claim.Id.ToString()); audit.Add("ActorId", scope.ActorId.ToString());
                    audit.Add("Version", claim.Version); audit.Add("Before", before); audit.Add("After", claim.ToBsonDocument());
                    audit.Add("CorrelationRoot", root.ToString()); audit.Add("OccurredAt", id.HasValue ? Text(occurredAt) : receivedAt.ToString("O"));
                    audit.Add("ReceivedAt", receivedAt.ToString("O")); audit.Add("CommittedAt", DateTimeOffset.UtcNow.ToString("O"));
                    audit.Add("ResolutionCode", Text(resolutionCode)); audit.Add("Note", Text(note));
                    await database.GetCollection<BsonDocument>("claims_audit").InsertOneAsync(session, audit, cancellationToken: ct); await probe.AtAsync("audit", ct);
                    var envelope = new { eventId, eventType = "Claim" + (id.HasValue ? claim.Status.ToString() : "Opened"),
                        occurredAt = id.HasValue ? occurredAt : receivedAt.ToString("O"), correlationId = root, causationId = (Guid?)null,
                        aggregateType = "Claim", aggregateId = claim.Id, payload = new { claimId = claim.Id, claimNumber = claim.ClaimNumber, shipmentId = claim.ShipmentId, status = claim.Status.ToString(), approvedAmount = claim.ApprovedAmount }, contractVersion = "v1" };
                    var pending = Scope(scope); pending.Add("_id", Guid.NewGuid().ToString()); pending.Add("ClaimId", claim.Id.ToString());
                    pending.Add("EventId", eventId.ToString()); pending.Add("Status", "Pending"); pending.Add("Envelope", BsonDocument.Parse(JsonSerializer.Serialize(envelope)));
                    await database.GetCollection<BsonDocument>("claims_outbox").InsertOneAsync(session, pending, cancellationToken: ct); await probe.AtAsync("outbox", ct);
                    await probe.AtAsync("beforeCommit", ct); commitAttempted = true;
                    for (var commit = 0; ; commit++)
                    {
                        try { await session.CommitTransactionAsync(ct); break; }
                        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }
                    }
                    await probe.AtAsync("afterCommit", ct);
                    return new(claim.Id, claim.ClaimNumber, claim.ShipmentId, claim.Status.ToString(), claim.ApprovedAmount, false, id.HasValue ? 200 : 201);
                }
                catch
                {
                    if (!commitAttempted && session.IsInTransaction)
                        try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { }
                    throw;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ClaimCasConflictException) { await Task.Delay(Math.Min(5 * (attempt + 1), 100), ct); }
            catch (ClaimFailureException ex) { return ClaimMutationResult.Error(ex.Status, ex.Code); }
            catch (MongoException ex) when (commitAttempted && ex.HasErrorLabel("TransientTransactionError") && !ex.HasErrorLabel("UnknownTransactionCommitResult"))
            { await Task.Delay(Math.Min(5 * (attempt + 1), 100), ct); }
            catch (MongoException ex) when (!commitAttempted && IsNumberCollision(ex))
            { return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE"); }
            catch (MongoException ex) when (!commitAttempted && (ex.HasErrorLabel("TransientTransactionError") || IsDuplicate(ex)))
            { await Task.Delay(Math.Min(5 * (attempt + 1), 100), ct); }
            catch (Exception ex) when (ex is MongoException or TimeoutException)
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
                return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE");
            }
            catch (FormatException ex) when (ex.Message.StartsWith("Size ", StringComparison.Ordinal) && ex.Message.Contains(" is larger than MaxDocumentSize ", StringComparison.Ordinal))
            { return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE"); }
            catch (MongoDB.Bson.BsonSerializationException ex) when (ex.Message.Contains("size", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("length", StringComparison.OrdinalIgnoreCase))
            { return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE"); }
            catch (Exception) { return ClaimMutationResult.Error(500, "INTERNAL_ERROR"); }
        }
        return ClaimMutationResult.Error(503, "CLAIM_STORAGE_UNAVAILABLE");
    }
    private sealed class ClaimCasConflictException : Exception { }
    private static BsonValue Text(string? value) => value is null ? BsonNull.Value : new BsonString(value);
    private static bool IsDuplicate(MongoException ex) => ex is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } || ex is MongoCommandException { Code: 11000 };
    private static bool IsNumberCollision(MongoException ex) => IsDuplicate(ex) && (ex.Message.Contains("claim_number", StringComparison.Ordinal) || ex.Message.Contains("_id_", StringComparison.Ordinal));
}
