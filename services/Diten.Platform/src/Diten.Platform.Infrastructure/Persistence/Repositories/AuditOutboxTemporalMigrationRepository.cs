using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class AuditOutboxTemporalMigrationRepository
{
    private readonly IMongoCollection<AuditOutboxTemporalMigrationState> _collection;
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly IMongoDatabase _database;

    public AuditOutboxTemporalMigrationRepository(IMongoDatabase database)
    {
        _database = database;
        _collection = database.GetCollection<AuditOutboxTemporalMigrationState>(
            AuditCollectionNames.AuditOutboxTemporalMigrations);
        _outbox = database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
    }

    public async Task<AuditOutboxTemporalMigrationState?> GetAsync(CancellationToken ct = default)
    {
        await ValidateInventoryAsync(ct);
        var state = await _collection.Find(x => x.Id == AuditOutboxTemporalMigrationState.ExactId).FirstOrDefaultAsync(ct);
        state?.Validate();
        return state;
    }

    public async Task ValidateInventoryAsync(CancellationToken ct = default)
    {
        var documents = await _collection.Find(FilterDefinition<AuditOutboxTemporalMigrationState>.Empty)
            .Project(x => x.Id)
            .ToListAsync(ct);
        if (documents.Count > 1 || (documents.Count == 1
            && !string.Equals(documents[0], AuditOutboxTemporalMigrationState.ExactId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_STATE_INVENTORY_INVALID");
        }
    }

    public async Task<AuditOutboxTemporalMigrationState> BeginPreflightAsync(
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        var candidate = new AuditOutboxTemporalMigrationState
        {
            Phase = AuditOutboxTemporalMigrationState.Phases.Preflight,
            StartedAtUtcTicks = nowUtcTicks,
            UpdatedAtUtcTicks = nowUtcTicks
        };
        candidate.Validate();

        try
        {
            await _collection.InsertOneAsync(candidate, cancellationToken: ct);
            return candidate;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await GetAsync(ct)
                   ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_STATE_DUPLICATE_UNREADABLE");
        }
    }

    public async Task<AuditOutboxTemporalMigrationState> CompletePreflightAsync(
        long scannedCount,
        long alreadyCurrentCount,
        string sourceFingerprint,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        if (scannedCount < 0 || alreadyCurrentCount < 0 || alreadyCurrentCount > scannedCount
            || string.IsNullOrWhiteSpace(sourceFingerprint) || sourceFingerprint.Length != 64)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_PREFLIGHT_FACTS_INVALID");
        }

        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Preflight),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.SourceFingerprint, string.Empty));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Ready)
            .Set(x => x.ScannedCount, scannedCount)
            .Set(x => x.AlreadyCurrentCount, alreadyCurrentCount)
            .Set(x => x.MigratedCount, 0)
            .Set(x => x.SourceFingerprint, sourceFingerprint)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks);
        return await _collection.FindOneAndUpdateAsync(
                   filter,
                   update,
                   new FindOneAndUpdateOptions<AuditOutboxTemporalMigrationState>
                   {
                       ReturnDocument = ReturnDocument.After
                   },
                   ct)
               ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_PREFLIGHT_STATE_CHANGED");
    }

    public async Task FailPreflightAsync(
        string failureCode,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Preflight));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Failed)
            .Set(x => x.FailureCode, string.IsNullOrWhiteSpace(failureCode)
                ? "AUDIT_OUTBOX_TEMPORAL_PREFLIGHT_FAILED"
                : failureCode[..Math.Min(failureCode.Length, 256)])
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_PREFLIGHT_STATE_CHANGED");
    }

    public async Task<AuditOutboxTemporalMigrationState> AcquireLeaseAsync(
        string leaseOwner,
        long nowUtcTicks,
        long leaseDurationTicks,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseOwner.Length > 128 || leaseDurationTicks <= 0)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_LEASE_FACTS_INVALID");
        }

        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Or(
                Builders<AuditOutboxTemporalMigrationState>.Filter.And(
                    Builders<AuditOutboxTemporalMigrationState>.Filter.In(
                        x => x.Phase,
                        new[]
                        {
                            AuditOutboxTemporalMigrationState.Phases.Ready,
                            AuditOutboxTemporalMigrationState.Phases.RecoveryRequired
                        }),
                    Builders<AuditOutboxTemporalMigrationState>.Filter.Or(
                        Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, null),
                        Builders<AuditOutboxTemporalMigrationState>.Filter.Lte(x => x.LeaseExpiresAtUtcTicks, nowUtcTicks))),
                Builders<AuditOutboxTemporalMigrationState>.Filter.And(
                    Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(
                        x => x.Phase,
                        AuditOutboxTemporalMigrationState.Phases.Migrating),
                    Builders<AuditOutboxTemporalMigrationState>.Filter.Lte(x => x.LeaseExpiresAtUtcTicks, nowUtcTicks))));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.LeaseOwner, leaseOwner)
            .Set(x => x.LeaseExpiresAtUtcTicks, checked(nowUtcTicks + leaseDurationTicks))
            .Inc(x => x.LeaseGeneration, 1)
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Migrating)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks)
            .Set(x => x.FailureCode, null);

        return await _collection.FindOneAndUpdateAsync(
                   filter,
                   update,
                   new FindOneAndUpdateOptions<AuditOutboxTemporalMigrationState>
                   {
                       ReturnDocument = ReturnDocument.After
                   },
                   ct)
               ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_LEASE_UNAVAILABLE");
    }

    public async Task<bool> TryMigrateLegacyRowUnderLeaseAsync(
        string leaseOwner,
        long generation,
        long nowUtcTicks,
        BsonDocument source,
        long nextAttemptAtUtcTicks,
        long createdAtUtcTicks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var session = await _database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(new TransactionOptions(
            readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority));
        try
        {
            var leaseFence = await _collection.UpdateOneAsync(
                session,
                BuildLeaseFilter(leaseOwner, generation, nowUtcTicks),
                Builders<AuditOutboxTemporalMigrationState>.Update
                    .Inc(x => x.LeaseFenceSequence, 1)
                    .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks),
                cancellationToken: ct);
            if (leaseFence.MatchedCount != 1)
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_LEASE_LOST");
            }

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", source["_id"]),
                Builders<BsonDocument>.Filter.Eq(nameof(AuditOutboxMessage.NextAttemptAtUtc), source[nameof(AuditOutboxMessage.NextAttemptAtUtc)]),
                Builders<BsonDocument>.Filter.Eq(nameof(AuditOutboxMessage.CreatedAtUtc), source[nameof(AuditOutboxMessage.CreatedAtUtc)]),
                Builders<BsonDocument>.Filter.Exists(nameof(AuditOutboxMessage.TemporalStorageVersion), false),
                Builders<BsonDocument>.Filter.Exists(nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1), false),
                Builders<BsonDocument>.Filter.Exists(nameof(AuditOutboxMessage.CreatedAtUtcTicksV1), false));
            var update = Builders<BsonDocument>.Update
                .Set(nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1), nextAttemptAtUtcTicks)
                .Set(nameof(AuditOutboxMessage.CreatedAtUtcTicksV1), createdAtUtcTicks)
                .Set(nameof(AuditOutboxMessage.TemporalStorageVersion), AuditOutboxTemporalMigrationState.ExactTargetVersion);
            var result = await _outbox.UpdateOneAsync(session, filter, update, cancellationToken: ct);
            await session.CommitTransactionAsync(ct);
            return result.MatchedCount == 1;
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            throw;
        }
    }

    public async Task SaveCheckpointAsync(
        string leaseOwner,
        long generation,
        Guid lastProcessedId,
        long migratedDelta,
        long alreadyCurrentDelta,
        long nowUtcTicks,
        long leaseDurationTicks,
        CancellationToken ct = default)
    {
        var filter = BuildLeaseFilter(leaseOwner, generation, nowUtcTicks);
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.LastProcessedId, lastProcessedId)
            .Inc(x => x.MigratedCount, migratedDelta)
            .Inc(x => x.AlreadyCurrentCount, alreadyCurrentDelta)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks)
            .Set(x => x.LeaseExpiresAtUtcTicks, checked(nowUtcTicks + leaseDurationTicks));
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_LEASE_LOST");
    }

    public async Task AssertLeaseAsync(
        string leaseOwner,
        long generation,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        var count = await _collection.CountDocumentsAsync(
            BuildLeaseFilter(leaseOwner, generation, nowUtcTicks),
            cancellationToken: ct);
        if (count != 1)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_LEASE_LOST");
        }
    }

    public async Task SetPhaseAsync(
        string leaseOwner,
        long generation,
        string phase,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        if (!AuditOutboxTemporalMigrationState.Phases.IsKnown(phase))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_PHASE_INVALID");
        }

        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, leaseOwner),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseGeneration, generation));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.Phase, phase)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks);

        if (phase == AuditOutboxTemporalMigrationState.Phases.RecoveryRequired)
        {
            update = update
                .Set(x => x.LeaseOwner, null)
                .Set(x => x.LeaseExpiresAtUtcTicks, null);
        }

        if (phase == AuditOutboxTemporalMigrationState.Phases.Completed)
        {
            update = update.Set(x => x.CompletedAtUtcTicks, nowUtcTicks);
        }

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_LEASE_LOST");
    }

    public async Task SetFinalCountsAndCompletedAsync(
        string leaseOwner,
        long generation,
        long migratedCount,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, leaseOwner),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseGeneration, generation),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Gt(x => x.LeaseExpiresAtUtcTicks, nowUtcTicks),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Gte(x => x.ScannedCount, migratedCount));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.MigratedCount, migratedCount)
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Completed)
            .Set(x => x.CompletedAtUtcTicks, nowUtcTicks)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_LEASE_LOST");
    }

    public async Task VerifyCompletionAndReleaseAsync(
        string leaseOwner,
        long generation,
        string selectedIndexName,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, leaseOwner),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseGeneration, generation),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Gt(x => x.LeaseExpiresAtUtcTicks, nowUtcTicks),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.Completed),
            new BsonDocumentFilterDefinition<AuditOutboxTemporalMigrationState>(
                new MongoDB.Bson.BsonDocument(
                    "$expr",
                    new MongoDB.Bson.BsonDocument(
                        "$eq",
                        new MongoDB.Bson.BsonArray
                        {
                            "$ScannedCount",
                            new MongoDB.Bson.BsonDocument(
                                "$add",
                                new MongoDB.Bson.BsonArray { "$MigratedCount", "$AlreadyCurrentCount" })
                        }))));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.CompletionVerified)
            .Set(x => x.SelectedIndexName, selectedIndexName)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks)
            .Set(x => x.LeaseOwner, null)
            .Set(x => x.LeaseExpiresAtUtcTicks, null);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_COMPLETION_NOT_VERIFIED");
    }

    public async Task ActivateAsync(
        string selectedIndexName,
        long nowUtcTicks,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(selectedIndexName) || selectedIndexName.Length > 128)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_INDEX_INVALID");
        }

        var filter = Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.CompletionVerified),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.SelectedIndexName, selectedIndexName),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, null));
        var update = Builders<AuditOutboxTemporalMigrationState>.Update
            .Set(x => x.Phase, AuditOutboxTemporalMigrationState.Phases.CutoverActive)
            .Set(x => x.ActivationVersion, AuditOutboxTemporalMigrationState.ExactTargetVersion)
            .Set(x => x.SelectedIndexName, selectedIndexName)
            .Set(x => x.UpdatedAtUtcTicks, nowUtcTicks);
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        EnsureMatched(result, "AUDIT_OUTBOX_TEMPORAL_ACTIVATION_BLOCKED");
    }

    private FilterDefinition<AuditOutboxTemporalMigrationState> BuildLeaseFilter(
        string leaseOwner,
        long generation,
        long nowUtcTicks) =>
        Builders<AuditOutboxTemporalMigrationState>.Filter.And(
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.Id, AuditOutboxTemporalMigrationState.ExactId),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseOwner, leaseOwner),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Eq(x => x.LeaseGeneration, generation),
            Builders<AuditOutboxTemporalMigrationState>.Filter.Gt(x => x.LeaseExpiresAtUtcTicks, nowUtcTicks));

    private static void EnsureMatched(UpdateResult result, string failureCode)
    {
        if (result.MatchedCount != 1)
        {
            throw new InvalidOperationException(failureCode);
        }
    }
}
