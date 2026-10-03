using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Persistence.Features.Carriers;
public sealed class CarrierRepository(IMongoDatabase db, ICarrierCommitProbe probe) : ICarrierRepository
{
    private readonly IMongoCollection<Carrier> _carriers = db.GetCollection<Carrier>("carriers").WithReadConcern(ReadConcern.Majority);
    private readonly IMongoCollection<BsonDocument> _receipts = db.GetCollection<BsonDocument>("carrier_idempotency");
    private static FilterDefinition<Carrier> Scope(CarrierScope scope, bool visible = true)
    {
        scope.EnsureTrusted();
        var filter = Builders<Carrier>.Filter.Eq(x => x.TenantId, scope.TenantId) & Builders<Carrier>.Filter.Eq(x => x.LegalEntityId, scope.LegalEntityId);
        return visible ? filter & Builders<Carrier>.Filter.Eq(x => x.IsDeleted, false) : filter;
    }
    public async Task<IReadOnlyList<Carrier>> QueryAsync(CarrierScope scope, CarrierStatus? status, CancellationToken ct)
    {
        var filter = Scope(scope);
        if (status is not null) filter &= Builders<Carrier>.Filter.Eq(x => x.Status, status.Value);
        try { return await _carriers.Find(filter, new FindOptions { Collation = Collation.Simple }).ToListAsync(ct); }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { throw new CarrierPersistenceUnavailableException(); }
    }
    public Task<CarrierMutationResult> CreateAsync(CarrierScope scope, string key, string fingerprint, Guid correlation, string code, string name,
        IReadOnlyList<string> modes, string? externalReference, CancellationToken ct) =>
        MutateAsync(scope, null, key, fingerprint, correlation, code, name, modes, externalReference, null, null, ct);
    public Task<CarrierMutationResult> ChangeStatusAsync(CarrierScope scope, Guid id, string key, string fingerprint, Guid correlation,
        CarrierStatus target, string reason, CancellationToken ct) => MutateAsync(scope, id, key, fingerprint, correlation, null, null, null, null, target, reason, ct);
    private static CarrierMutationResult Error(int status, string code) => new(Guid.Empty, "", "", false, status, code);
    private async Task<CarrierMutationResult> MutateAsync(CarrierScope scope, Guid? id, string key, string fingerprint, Guid correlation,
        string? code, string? name, IReadOnlyList<string>? modes, string? externalReference, CarrierStatus? target, string? reason, CancellationToken ct)
    {
        var scoped = Scope(scope);
        var operation = id is null ? "createCarrier" : "changeCarrierStatus";
        var receiptFilter = new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() },
            { "Operation", operation }, { "TargetId", id?.ToString() ?? "create" }, { "IdempotencyKey", key } };
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var commitAttempted = false;
            try
            {
                using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority, maxCommitTime: TimeSpan.FromSeconds(5)));
                try
                {
                    var receipt = await _receipts.Find(session, receiptFilter, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
                    if (receipt is not null)
                    {
                        await session.AbortTransactionAsync(ct);
                        return receipt["Fingerprint"].AsString != fingerprint ? Error(409, "IDEMPOTENCY_KEY_REUSED") :
                            new(Guid.Parse(receipt["CarrierId"].AsString), receipt["CarrierCode"].AsString, receipt["ResultingStatus"].AsString, true, receipt["StatusCode"].AsInt32);
                    }
                    Carrier carrier;
                    string? oldStatus = null;
                    if (id is null)
                    {
                        if (await _carriers.Find(session, Scope(scope, false) & Builders<Carrier>.Filter.Eq(x => x.CarrierCode, code), new FindOptions { Collation = Collation.Simple }).AnyAsync(ct))
                        { await session.AbortTransactionAsync(ct); return Error(409, "CARRIER_CODE_CONFLICT"); }
                        carrier = new() { Id = Guid.NewGuid(), TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CarrierCode = code!,
                            DisplayName = name!, SupportedModes = modes!.ToList(), ExternalReference = externalReference, Status = CarrierStatus.Active,
                            Version = 1, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = scope.ActorId };
                        await _carriers.InsertOneAsync(session, carrier, cancellationToken: ct);
                    }
                    else
                    {
                        var current = await _carriers.Find(session, scoped & Builders<Carrier>.Filter.Eq(x => x.Id, id.Value)).FirstOrDefaultAsync(ct);
                        if (current is null) { await session.AbortTransactionAsync(ct); return Error(404, "CARRIER_NOT_FOUND"); }
                        if (!CarrierLifecycle.Allows(current.Status, target!.Value)) { await session.AbortTransactionAsync(ct); return Error(422, "INVALID_CARRIER_TRANSITION"); }
                        carrier = current; oldStatus = current.Status.ToString(); var version = current.Version;
                        carrier.Status = target.Value; carrier.Version++; carrier.UpdatedAt = DateTimeOffset.UtcNow; carrier.UpdatedBy = scope.ActorId;
                        var updated = await _carriers.ReplaceOneAsync(session, scoped & Builders<Carrier>.Filter.Eq(x => x.Id, id.Value) & Builders<Carrier>.Filter.Eq(x => x.Version, version), carrier, cancellationToken: ct);
                        if (updated.ModifiedCount != 1) throw new InvalidOperationException("Carrier version predicate failed.");
                    }
                    await probe.AtAsync("entity", ct);
                    var status = id is null ? 201 : 200;
                    var saved = receiptFilter.DeepClone().AsBsonDocument;
                    saved.Add("_id", Guid.NewGuid().ToString()); saved.Add("Fingerprint", fingerprint); saved.Add("FormatVersion", 1);
                    saved.Add("CarrierId", carrier.Id.ToString()); saved.Add("CarrierCode", carrier.CarrierCode); saved.Add("ResultingStatus", carrier.Status.ToString());
                    saved.Add("StatusCode", status); saved.Add("ContractVersion", "v1"); saved.Add("CorrelationId", correlation.ToString()); saved.Add("ActorId", scope.ActorId.ToString());
                    await _receipts.InsertOneAsync(session, saved, cancellationToken: ct);
                    await probe.AtAsync("receipt", ct);
                    await db.GetCollection<BsonDocument>("carrier_audit").InsertOneAsync(session, new BsonDocument {
                        { "_id", Guid.NewGuid().ToString() }, { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() },
                        { "CarrierId", carrier.Id.ToString() }, { "Operation", operation }, { "Version", carrier.Version }, { "FromStatus", oldStatus is null ? BsonNull.Value : oldStatus },
                        { "ToStatus", carrier.Status.ToString() }, { "ReasonCode", reason is null ? BsonNull.Value : reason }, { "ActorId", scope.ActorId.ToString() },
                        { "CorrelationId", correlation.ToString() }, { "OccurredAt", DateTime.UtcNow } }, cancellationToken: ct);
                    await probe.AtAsync("audit", ct); await probe.AtAsync("beforeCommit", ct);
                    commitAttempted = true;
                    for (var commit = 0; ; commit++)
                    {
                        try { await session.CommitTransactionAsync(ct); break; }
                        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }
                    }
                    // A post-commit failure never triggers abort or a fresh mutation retry here.
                    await probe.AtAsync("afterCommit", ct);
                    return new(carrier.Id, carrier.CarrierCode, carrier.Status.ToString(), false, status);
                }
                catch
                {
                    if (!commitAttempted && session.IsInTransaction)
                        try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { }
                    throw;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (MongoException ex) when (!commitAttempted && (ex.HasErrorLabel("TransientTransactionError") || ex is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }))
            { await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct); }
            catch (Exception ex) when (ex is MongoException or TimeoutException or CarrierPersistenceUnavailableException) { return Error(503, "PERSISTENCE_UNAVAILABLE"); }
            catch (Exception) { return Error(500, "INTERNAL_ERROR"); }
        }
        return Error(503, "PERSISTENCE_UNAVAILABLE");
    }
}
