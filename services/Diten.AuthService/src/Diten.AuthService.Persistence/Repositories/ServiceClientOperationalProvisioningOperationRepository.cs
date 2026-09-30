using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ServiceClientOperationalProvisioningOperationRepository
    : IServiceClientOperationalProvisioningOperationRepository
{
    private const string Journal = "serviceClientOperationalProvisioningOperations";
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ServiceClientOperationalProvisioningOperation> _collection;
    private BsonBinaryData? _journalUuid;

    public ServiceClientOperationalProvisioningOperationRepository(IMongoDatabase database)
    {
        _database = database;
        _collection = database.GetCollection<ServiceClientOperationalProvisioningOperation>(Journal);
    }

    public Task VerifyStorageAsync(CancellationToken ct) => ExecuteAsync(async () =>
    {
        _journalUuid = null;
        var identity = await RequireCollectionAsync("serviceClientIdentities", ct);
        var grants = await RequireCollectionAsync("serviceClientTenantGrants", ct);
        var journal = await RequireCollectionAsync(Journal, ct);
        await RequireIndexesAsync("serviceClientIdentities", ct,
            new RequiredIndex("ux_service_client_identity_code_active", new("ClientCode", 1), true, true));
        await RequireIndexesAsync("serviceClientTenantGrants", ct,
            new RequiredIndex("ux_service_client_grant_tenant_client_audience_active",
                new() { { "TenantId", 1 }, { "ServiceClientIdentityId", 1 }, { "Audience", 1 } }, true, true),
            new RequiredIndex("ix_service_client_grant_tenant_enabled",
                new() { { "TenantId", 1 }, { "IsEnabled", 1 } }, false, false));
        await RequireIndexesAsync(Journal, ct,
            new RequiredIndex("ux_service_client_operational_command_active", new("CommandId", 1), true, true),
            new RequiredIndex("ix_service_client_operational_state_updated",
                new() { { "State", 1 }, { "UpdatedAt", 1 } }, false, false));
        // Detect replacement during index inspection; reserve also binds the UUID at the server.
        if (!identity.Equals(await RequireCollectionAsync("serviceClientIdentities", ct))
            || !grants.Equals(await RequireCollectionAsync("serviceClientTenantGrants", ct))
            || !journal.Equals(await RequireCollectionAsync(Journal, ct)))
            throw Unavailable();
        _journalUuid = journal;
        return true;
    });

    public Task<ServiceClientOperationalProvisioningOperation?> GetByCommandIdAsync(Guid commandId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            // Include tombstones and fail closed for contradictory legacy duplicates.
            var rows = await _collection.Find(x => x.CommandId == commandId).Limit(2).ToListAsync(ct);
            if (rows.Count > 1) throw Unavailable();
            return rows.SingleOrDefault();
        });

    public async Task<bool> TryReserveAsync(ServiceClientOperationalProvisioningOperation operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.CommandId == Guid.Empty || operation.IsDeleted
            || operation.State != ServiceClientOperationalProvisioningState.Pending
            || operation.Checkpoint != ServiceClientOperationalProvisioningCheckpoint.Reserved
            || operation.RecordedOutcome is not null || operation.EvidenceSequence != 1 || operation.Evidence.Count != 1)
            throw new ArgumentException("Invalid operational reservation.", nameof(operation));
        await VerifyStorageAsync(ct);
        if (await GetByCommandIdAsync(operation.CommandId, ct) is not null) return false;
        var document = operation.ToBsonDocument();
        // Reuse the existing _id uniqueness fence even after a new-format command is tombstoned.
        document["_id"] = new BsonBinaryData(operation.CommandId, GuidRepresentation.Standard);
        var command = new BsonDocument
        {
            { "insert", Journal }, { "collectionUUID", _journalUuid! },
            { "documents", new BsonArray { document } }, { "ordered", true },
            { "writeConcern", new BsonDocument { { "w", "majority" } } }
        };
        try
        {
            // MongoDB 7.0 write_ops.idl + write_ops_exec.cpp: expectedUUID is checked while acquiring
            // the collection, BEFORE implicit-create. Unsupported servers fail; never retry without UUID.
            var response = await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct);
            if (response.Contains("writeConcernError")) throw Unavailable();
            if (response.TryGetValue("writeErrors", out var errors) && errors.AsBsonArray.Count != 0)
            {
                if (errors.AsBsonArray.All(x => x.AsBsonDocument.GetValue("code", 0) == 11000)) return false;
                throw Unavailable();
            }
            if (response.GetValue("n", 0) != 1) throw Unavailable();
            var persisted = await GetByCommandIdAsync(operation.CommandId, ct);
            if (persisted is null || !persisted.ToBsonDocument().Equals(document)) throw Unavailable();
            return true;
        }
        catch (MongoException ex)
        {
            // Lost acknowledgement cannot grant a second execution. A persisted pending reservation
            // is returned as existing, which the service sends to manual recovery (never resume).
            if (!ct.IsCancellationRequested)
            {
                var persisted = await GetByCommandIdAsync(operation.CommandId, ct);
                if (persisted is not null) return false;
            }
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }

    public Task<bool> TryCompleteAsync(Guid commandId, string fingerprint,
        ServiceClientOperationalRecordedOutcome outcome, DateTimeOffset nowUtc, CancellationToken ct) => ExecuteAsync(async () =>
    {
        var operation = await GetByCommandIdAsync(commandId, ct);
        if (operation is null || operation.IsDeleted || operation.CommandFingerprint != fingerprint
            || operation.State != ServiceClientOperationalProvisioningState.Pending
            || operation.Checkpoint != ServiceClientOperationalProvisioningCheckpoint.Reserved
            || operation.RecordedOutcome is not null || operation.ExpectedOperationalVersion == long.MaxValue
            || outcome.ServiceClientIdentityId != operation.TargetId || outcome.ClientCode != operation.ClientCode
            || outcome.ServiceName != operation.ServiceName || outcome.Audience != operation.Audience
            || outcome.OperationalVersion != operation.ExpectedOperationalVersion + 1
            || outcome.CredentialVersion != $"op-{commandId:N}" || outcome.MutationObservedAtUtc == default
            || outcome.MutationObservedAtUtc.Offset != TimeSpan.Zero || nowUtc.Offset != TimeSpan.Zero)
            return false;
        var f = Builders<ServiceClientOperationalProvisioningOperation>.Filter;
        var filter = f.And(PendingFilter(commandId, fingerprint),
            f.Eq(x => x.Id, operation.Id), f.Eq(x => x.TargetId, outcome.ServiceClientIdentityId),
            f.Eq(x => x.Operation, operation.Operation), f.Eq(x => x.TenantId, operation.TenantId),
            f.Eq(x => x.ClientCode, outcome.ClientCode), f.Eq(x => x.ServiceName, outcome.ServiceName),
            f.Eq(x => x.Audience, outcome.Audience), f.Eq(x => x.ActorId, operation.ActorId),
            f.Eq(x => x.ExpectedOperationalVersion, operation.ExpectedOperationalVersion),
            f.Eq(x => x.ExpectedCredentialVersion, operation.ExpectedCredentialVersion),
            f.Eq(x => x.EvidenceSequence, 1));
        var evidence = new ServiceClientOperationalProvisioningEvidence
        {
            Sequence = 2, EventType = "credential-readback-recorded", OccurredAtUtc = nowUtc,
            ActorId = operation.ActorId, TargetOperationalVersion = outcome.OperationalVersion
        };
        var update = Builders<ServiceClientOperationalProvisioningOperation>.Update
            .Set(x => x.RecordedOutcome, outcome)
            .Set(x => x.State, ServiceClientOperationalProvisioningState.Completed)
            .Set(x => x.Checkpoint, ServiceClientOperationalProvisioningCheckpoint.EvidenceRecorded)
            .Set(x => x.UpdatedAt, nowUtc).Set(x => x.UpdatedBy, operation.ActorId)
            .Set(x => x.EvidenceSequence, 2).Push(x => x.Evidence, evidence);
        var result = await _collection.UpdateOneAsync(filter, update,
            new UpdateOptions { IsUpsert = false, Collation = Collation.Simple }, ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    });

    public Task<bool> TryMarkRecoveryRequiredAsync(Guid commandId, string fingerprint, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var result = await _collection.UpdateOneAsync(PendingFilter(commandId, fingerprint),
                Builders<ServiceClientOperationalProvisioningOperation>.Update
                    .Set(x => x.State, ServiceClientOperationalProvisioningState.RecoveryRequired),
                new UpdateOptions { IsUpsert = false, Collation = Collation.Simple }, ct);
            return result.IsAcknowledged && result.ModifiedCount == 1;
        });

    private static FilterDefinition<ServiceClientOperationalProvisioningOperation> PendingFilter(Guid commandId, string fingerprint)
    {
        var f = Builders<ServiceClientOperationalProvisioningOperation>.Filter;
        return f.And(f.Eq(x => x.CommandId, commandId), f.Eq(x => x.CommandFingerprint, fingerprint),
            f.Eq(x => x.IsDeleted, false), f.Eq(x => x.State, ServiceClientOperationalProvisioningState.Pending),
            f.Eq(x => x.Checkpoint, ServiceClientOperationalProvisioningCheckpoint.Reserved),
            f.Eq(x => x.RecordedOutcome, null));
    }

    private async Task<BsonBinaryData> RequireCollectionAsync(string name, CancellationToken ct)
    {
        using var cursor = await _database.ListCollectionsAsync(
            new ListCollectionsOptions { Filter = new BsonDocument("name", name) }, ct);
        var rows = await cursor.ToListAsync(ct);
        if (rows.Count != 1 || rows[0].GetValue("type", "") != "collection"
            || !rows[0].TryGetValue("info", out var info) || !info.IsBsonDocument
            || !info.AsBsonDocument.TryGetValue("uuid", out var uuid) || !uuid.IsBsonBinaryData
            || uuid.AsBsonBinaryData.SubType != BsonBinarySubType.UuidStandard)
            throw Unavailable();
        return uuid.AsBsonBinaryData;
    }

    private async Task RequireIndexesAsync(string collection, CancellationToken ct, params RequiredIndex[] expected)
    {
        using var cursor = await _database.GetCollection<BsonDocument>(collection).Indexes.ListAsync(ct);
        var indexes = await cursor.ToListAsync(ct);
        foreach (var spec in expected)
        {
            var matches = indexes.Where(x => x.GetValue("name", "") == spec.Name).ToArray();
            if (matches.Length != 1) throw Unavailable();
            var index = matches[0];
            var partial = spec.ActiveOnly ? new BsonDocument("IsDeleted", false) : null;
            if (!index.GetValue("key", BsonNull.Value).Equals(spec.Keys)
                || index.GetValue("unique", false) != spec.Unique
                || index.GetValue("sparse", false) != false || index.GetValue("hidden", false) != false
                || index.Contains("expireAfterSeconds") || index.Contains("collation")
                || (partial is null ? index.Contains("partialFilterExpression")
                    : !index.GetValue("partialFilterExpression", BsonNull.Value).Equals(partial)))
                throw Unavailable();
        }
    }

    private sealed record RequiredIndex(string Name, BsonDocument Keys, bool Unique, bool ActiveOnly);
    private static ServiceIdentityPersistenceUnavailableException Unavailable() =>
        new(new InvalidOperationException("Existing operational storage is unavailable or inconsistent."));

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is MongoException or BsonSerializationException or FormatException)
        {
            throw new ServiceIdentityPersistenceUnavailableException(ex);
        }
    }
}
