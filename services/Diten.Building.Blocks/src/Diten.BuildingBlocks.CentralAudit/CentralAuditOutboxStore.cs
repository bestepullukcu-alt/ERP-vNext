using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// The service's own central-audit outbox. Append and status changes only: there is no member that deletes a row (a
/// delivery failure leaves the row where it is — AUD-001 §4.1 spirit, and the "never lost" half of BL-501).
/// </summary>
public interface ICentralAuditOutboxStore
{
    /// <summary>Writes the row inside the caller's Mongo transaction; a second call for the same id in it is a no-op.</summary>
    Task InsertInTransactionAsync(IClientSessionHandle session, CentralAuditOutboxRecord record, CancellationToken ct);

    /// <summary>Writes the row outside any transaction (the command had none); a second call for the same id is a no-op.</summary>
    Task InsertAsync(CentralAuditOutboxRecord record, CancellationToken ct);

    Task SetEntityIdIfMissingAsync(Guid id, Guid entityId, CancellationToken ct);

    /// <summary>The command ended: an <see cref="CentralAuditOutboxStatus.AwaitingCommand"/> row becomes deliverable with this outcome.</summary>
    Task ConfirmAsync(Guid id, string outcome, CancellationToken ct);

    /// <summary>Rows still awaiting their command after the cut-off become deliverable (their transaction committed). Returns how many.</summary>
    Task<long> ReleaseOrphansAsync(DateTime createdBeforeUtc, CancellationToken ct);

    Task<IReadOnlyList<CentralAuditOutboxRecord>> GetDueAsync(DateTime nowUtc, int batchSize, CancellationToken ct);

    Task MarkDeliveredAsync(Guid id, DateTime nowUtc, CancellationToken ct);

    Task MarkRetryAsync(Guid id, int attempts, DateTime nextAttemptAtUtc, string error, CancellationToken ct);

    Task MarkRejectedAsync(Guid id, int attempts, string error, CancellationToken ct);

    Task<CentralAuditBacklog> GetBacklogAsync(CancellationToken ct);

    Task EnsureIndexesAsync(CancellationToken ct);
}

/// <summary>What is still in the outbox: waiting rows, the oldest one's age, and rows Platform refused.</summary>
public sealed record CentralAuditBacklog(long Pending, DateTime? OldestPendingCreatedAtUtc, long Rejected);

public sealed class MongoCentralAuditOutboxStore : ICentralAuditOutboxStore
{
    private static readonly object MapGate = new();
    private readonly IMongoCollection<CentralAuditOutboxRecord> _collection;

    public MongoCentralAuditOutboxStore(IMongoDatabase database)
    {
        EnsureClassMap();
        _collection = database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName);
    }

    public async Task InsertInTransactionAsync(IClientSessionHandle session, CentralAuditOutboxRecord record, CancellationToken ct)
    {
        // Read-then-insert, not insert-and-catch: a duplicate-key error inside a transaction aborts the whole transaction,
        // and a command that enlists twice (two postings under one command) must not abort its own business write.
        if (await _collection.Find(session, x => x.Id == record.Id).AnyAsync(ct))
        {
            return;
        }

        await _collection.InsertOneAsync(session, record, cancellationToken: ct);
    }

    public async Task InsertAsync(CentralAuditOutboxRecord record, CancellationToken ct)
    {
        try
        {
            await _collection.InsertOneAsync(record, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Already there (written in the command's transaction, or a retried write) — the event exists once.
        }
    }

    public Task SetEntityIdIfMissingAsync(Guid id, Guid entityId, CancellationToken ct) =>
        _collection.UpdateOneAsync(
            x => x.Id == id && x.EntityId == null,
            Builders<CentralAuditOutboxRecord>.Update.Set(x => x.EntityId, entityId),
            cancellationToken: ct);

    public Task ConfirmAsync(Guid id, string outcome, CancellationToken ct) =>
        _collection.UpdateOneAsync(
            x => x.Id == id && x.Status == CentralAuditOutboxStatus.AwaitingCommand,
            Builders<CentralAuditOutboxRecord>.Update
                .Set(x => x.Status, CentralAuditOutboxStatus.Pending)
                .Set(x => x.Outcome, outcome),
            cancellationToken: ct);

    public async Task<long> ReleaseOrphansAsync(DateTime createdBeforeUtc, CancellationToken ct)
    {
        var result = await _collection.UpdateManyAsync(
            x => x.Status == CentralAuditOutboxStatus.AwaitingCommand && x.CreatedAtUtc < createdBeforeUtc,
            Builders<CentralAuditOutboxRecord>.Update
                .Set(x => x.Status, CentralAuditOutboxStatus.Pending)
                .Set(x => x.LastError, "Released by the dispatcher: the command never confirmed this row (its transaction committed)."),
            cancellationToken: ct);
        return result.ModifiedCount;
    }

    public async Task<IReadOnlyList<CentralAuditOutboxRecord>> GetDueAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
        await _collection
            .Find(x => x.Status == CentralAuditOutboxStatus.Pending && x.NextAttemptAtUtc <= nowUtc)
            .SortBy(x => x.NextAttemptAtUtc)
            .Limit(batchSize)
            .ToListAsync(ct);

    public Task MarkDeliveredAsync(Guid id, DateTime nowUtc, CancellationToken ct) =>
        _collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<CentralAuditOutboxRecord>.Update
                .Set(x => x.Status, CentralAuditOutboxStatus.Delivered)
                .Set(x => x.DeliveredAtUtc, nowUtc)
                .Inc(x => x.Attempts, 1)
                .Set(x => x.LastError, null),
            cancellationToken: ct);

    public Task MarkRetryAsync(Guid id, int attempts, DateTime nextAttemptAtUtc, string error, CancellationToken ct) =>
        _collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<CentralAuditOutboxRecord>.Update
                .Set(x => x.Attempts, attempts)
                .Set(x => x.NextAttemptAtUtc, nextAttemptAtUtc)
                .Set(x => x.LastError, error),
            cancellationToken: ct);

    public Task MarkRejectedAsync(Guid id, int attempts, string error, CancellationToken ct) =>
        _collection.UpdateOneAsync(
            x => x.Id == id,
            Builders<CentralAuditOutboxRecord>.Update
                .Set(x => x.Status, CentralAuditOutboxStatus.Rejected)
                .Set(x => x.Attempts, attempts)
                .Set(x => x.LastError, error),
            cancellationToken: ct);

    public async Task<CentralAuditBacklog> GetBacklogAsync(CancellationToken ct)
    {
        var pending = await _collection.CountDocumentsAsync(x => x.Status == CentralAuditOutboxStatus.Pending, cancellationToken: ct);
        var rejected = await _collection.CountDocumentsAsync(x => x.Status == CentralAuditOutboxStatus.Rejected, cancellationToken: ct);
        var oldest = pending == 0
            ? null
            : await _collection.Find(x => x.Status == CentralAuditOutboxStatus.Pending)
                .SortBy(x => x.CreatedAtUtc)
                .Project(x => (DateTime?)x.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
        return new CentralAuditBacklog(pending, oldest, rejected);
    }

    public Task EnsureIndexesAsync(CancellationToken ct) =>
        _collection.Indexes.CreateManyAsync(
            [
                // The dispatcher's queue read — cross-tenant by nature (one queue per service), like Platform's
                // ix_audit_outbox_status_next_attempt.
                new CreateIndexModel<CentralAuditOutboxRecord>(
                    Builders<CentralAuditOutboxRecord>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.NextAttemptAtUtc),
                    new CreateIndexOptions { Name = "ix_central_audit_outbox_status_next_attempt" }),
                // Tenant-first (mongo-indexing.md): "what did this tenant's commands queue", like ix_audit_outbox_tenant_created.
                new CreateIndexModel<CentralAuditOutboxRecord>(
                    Builders<CentralAuditOutboxRecord>.IndexKeys.Ascending(x => x.TenantId).Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions { Name = "ix_central_audit_outbox_tenant_created" })
            ],
            ct);

    /// <summary>
    /// The row's BSON shape belongs to this package, not to the host service: services register different global Guid
    /// serializers (Inventory: string), and a row one service writes must read the same everywhere.
    /// </summary>
    internal static void EnsureClassMap()
    {
        lock (MapGate)
        {
            if (BsonClassMap.IsClassMapRegistered(typeof(CentralAuditOutboxRecord)))
            {
                return;
            }

            var guid = new GuidSerializer(GuidRepresentation.Standard);
            var nullableGuid = new NullableSerializer<Guid>(guid);
            var utc = new DateTimeSerializer(DateTimeKind.Utc);
            BsonClassMap.RegisterClassMap<CentralAuditOutboxRecord>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
                map.MapIdMember(x => x.Id).SetSerializer(guid);
                map.MapMember(x => x.TenantId).SetSerializer(guid);
                map.MapMember(x => x.CorrelationId).SetSerializer(guid);
                map.MapMember(x => x.CompanyId).SetSerializer(nullableGuid);
                map.MapMember(x => x.ActorId).SetSerializer(nullableGuid);
                map.MapMember(x => x.EntityId).SetSerializer(nullableGuid);
                map.MapMember(x => x.OccurredAtUtc).SetSerializer(utc);
                map.MapMember(x => x.NextAttemptAtUtc).SetSerializer(utc);
                map.MapMember(x => x.CreatedAtUtc).SetSerializer(utc);
                map.MapMember(x => x.DeliveredAtUtc).SetSerializer(new NullableSerializer<DateTime>(utc));
                map.MapMember(x => x.Status).SetSerializer(new EnumSerializer<CentralAuditOutboxStatus>(BsonType.String));
            });
        }
    }
}
