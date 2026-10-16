using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Migrations;

public sealed class AuditOutboxTemporalStorageMigrationRunner
{
    public const string BeforeFirstBatch = "before-first-batch";
    public const string AfterRowBeforeCheckpoint = "after-row-before-checkpoint";
    public const string AfterCheckpoint = "after-checkpoint";
    public const string AfterCompletedBeforeVerification = "after-completed-before-verification";
    public const string ScalarClaimIndexName = AuditOutboxTemporalMigrationState.ExactScalarClaimIndexName;

    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly AuditOutboxTemporalMigrationRepository _stateRepository;
    private readonly TimeProvider _timeProvider;

    internal Func<string, CancellationToken, Task>? FailureInjector { get; set; }

    public AuditOutboxTemporalStorageMigrationRunner(
        IMongoDatabase database,
        AuditOutboxTemporalMigrationRepository stateRepository,
        TimeProvider timeProvider)
    {
        _outbox = database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        _stateRepository = stateRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AuditOutboxTemporalMigrationState> PreflightAsync(CancellationToken ct = default)
    {
        await EnsureTransactionCapableReplicaSetAsync(ct);
        await _stateRepository.ValidateInventoryAsync(ct);
        var state = await _stateRepository.GetAsync(ct);
        if (state is not null && state.Phase != AuditOutboxTemporalMigrationState.Phases.Preflight)
        {
            return state;
        }

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        state ??= await _stateRepository.BeginPreflightAsync(nowTicks, ct);

        var documents = await _outbox.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToListAsync(ct);

        long alreadyCurrent = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            foreach (var document in documents)
            {
                _ = ReadGuidId(document);
                var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);
                if (inspection.Kind == AuditOutboxTemporalStorageCompatibility.InspectionKind.Malformed)
                {
                    throw new InvalidOperationException(inspection.FailureCode);
                }

                if (inspection.Kind == AuditOutboxTemporalStorageCompatibility.InspectionKind.Current)
                {
                    alreadyCurrent++;
                }

                var id = document.GetValue("_id", BsonNull.Value).ToJson();
                var fact = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{id}|{inspection.NextAttemptAtUtcTicks}|{inspection.CreatedAtUtcTicks}\n");
                hash.AppendData(Encoding.UTF8.GetBytes(fact));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _stateRepository.FailPreflightAsync(ex.Message, _timeProvider.GetUtcNow().UtcTicks, ct);
            throw;
        }

        var fingerprint = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return await _stateRepository.CompletePreflightAsync(
            documents.Count,
            alreadyCurrent,
            fingerprint,
            nowTicks,
            ct);
    }

    public async Task<AuditOutboxTemporalMigrationState> RunAsync(
        string migrationId,
        int targetVersion,
        int batchSize,
        TimeSpan leaseDuration,
        string leaseOwner,
        bool activateScalarClaims,
        string selectedIndexName,
        CancellationToken ct = default)
    {
        ValidateRunFacts(migrationId, targetVersion, batchSize, leaseDuration, leaseOwner, selectedIndexName);
        await EnsureTransactionCapableReplicaSetAsync(ct);
        if (!await ExactIndexExistsAsync(selectedIndexName, ct))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID");
        }
        var existing = await _stateRepository.GetAsync(ct);
        var preflight = existing is null || existing.Phase == AuditOutboxTemporalMigrationState.Phases.Preflight
            ? await PreflightAsync(ct)
            : existing;
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;

        if (preflight.Phase == AuditOutboxTemporalMigrationState.Phases.CutoverActive)
        {
            if (preflight.ActivationVersion != AuditOutboxTemporalStorageCompatibility.CurrentVersion
                || !string.Equals(preflight.SelectedIndexName, ScalarClaimIndexName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ACTIVE_STATE_INVALID");
            }

            if (!string.Equals(selectedIndexName, ScalarClaimIndexName, StringComparison.Ordinal)
                || !await ExactIndexExistsAsync(selectedIndexName, ct))
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID");
            }

            return preflight;
        }

        if (preflight.Phase == AuditOutboxTemporalMigrationState.Phases.CompletionVerified)
        {
            if (!string.Equals(preflight.SelectedIndexName, selectedIndexName, StringComparison.Ordinal)
                || !await ExactIndexExistsAsync(selectedIndexName, ct))
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID");
            }

            if (activateScalarClaims)
            {
                await ActivateAsync(selectedIndexName, nowTicks, ct);
                return await _stateRepository.GetAsync(ct)
                       ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_STATE_MISSING");
            }

            return preflight;
        }

        var lease = await _stateRepository.AcquireLeaseAsync(
            leaseOwner,
            nowTicks,
            leaseDuration.Ticks,
            ct);

        try
        {
            if (FailureInjector is not null)
            {
                await FailureInjector(BeforeFirstBatch, ct);
            }

            var lastProcessedId = lease.LastProcessedId;
            while (true)
            {
                var filter = lastProcessedId.HasValue
                    ? Builders<BsonDocument>.Filter.Gt("_id", lastProcessedId.Value)
                    : FilterDefinition<BsonDocument>.Empty;
                var batch = await _outbox.Find(filter)
                    .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                    .Limit(batchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var document in batch)
                {
                    var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);
                    if (inspection.Kind == AuditOutboxTemporalStorageCompatibility.InspectionKind.Malformed)
                    {
                        throw new InvalidOperationException(inspection.FailureCode);
                    }

                    if (inspection.Kind == AuditOutboxTemporalStorageCompatibility.InspectionKind.Legacy)
                    {
                        await MigrateOneAsync(
                            leaseOwner,
                            lease.LeaseGeneration,
                            _timeProvider.GetUtcNow().UtcTicks,
                            document,
                            inspection,
                            ct);
                        if (FailureInjector is not null)
                        {
                            await FailureInjector(AfterRowBeforeCheckpoint, ct);
                        }
                    }

                    lastProcessedId = ReadGuidId(document);
                }

                nowTicks = _timeProvider.GetUtcNow().UtcTicks;
                await _stateRepository.SaveCheckpointAsync(
                    leaseOwner,
                    lease.LeaseGeneration,
                    lastProcessedId!.Value,
                    migratedDelta: 0,
                    alreadyCurrentDelta: 0,
                    nowTicks,
                    leaseDuration.Ticks,
                    ct);
                if (FailureInjector is not null)
                {
                    await FailureInjector(AfterCheckpoint, ct);
                }
            }

            var remaining = await CountNotCurrentAsync(ct);
            if (remaining != 0)
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_COMPLETION_HAS_UNMIGRATED_ROWS");
            }

            if (!string.Equals(selectedIndexName, ScalarClaimIndexName, StringComparison.Ordinal)
                || !await ExactIndexExistsAsync(selectedIndexName, ct))
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID");
            }

            nowTicks = _timeProvider.GetUtcNow().UtcTicks;
            await _stateRepository.SetFinalCountsAndCompletedAsync(
                leaseOwner,
                lease.LeaseGeneration,
                preflight.ScannedCount - preflight.AlreadyCurrentCount,
                nowTicks,
                ct);
            if (FailureInjector is not null)
            {
                await FailureInjector(AfterCompletedBeforeVerification, ct);
            }

            await _stateRepository.VerifyCompletionAndReleaseAsync(
                leaseOwner,
                lease.LeaseGeneration,
                selectedIndexName,
                nowTicks,
                ct);
        }
        catch
        {
            try
            {
                await _stateRepository.SetPhaseAsync(
                    leaseOwner,
                    lease.LeaseGeneration,
                    AuditOutboxTemporalMigrationState.Phases.RecoveryRequired,
                    _timeProvider.GetUtcNow().UtcTicks,
                    CancellationToken.None);
            }
            catch
            {
                // The durable lease may have been lost. Preserve the original failure.
            }

            throw;
        }

        if (activateScalarClaims)
        {
            await ActivateAsync(selectedIndexName, nowTicks, ct);
        }

        return await _stateRepository.GetAsync(ct)
               ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_STATE_MISSING");
    }

    private async Task ActivateAsync(string selectedIndexName, long nowTicks, CancellationToken ct)
    {
        if (!string.Equals(selectedIndexName, ScalarClaimIndexName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID");
        }

        if (!await ExactIndexExistsAsync(selectedIndexName, ct))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_MISSING");
        }

        await _stateRepository.ActivateAsync(selectedIndexName, nowTicks, ct);
    }

    private async Task MigrateOneAsync(
        string leaseOwner,
        long generation,
        long nowUtcTicks,
        BsonDocument source,
        AuditOutboxTemporalStorageCompatibility.Inspection inspection,
        CancellationToken ct)
    {
        var migrated = await _stateRepository.TryMigrateLegacyRowUnderLeaseAsync(
            leaseOwner,
            generation,
            nowUtcTicks,
            source,
            inspection.NextAttemptAtUtcTicks,
            inspection.CreatedAtUtcTicks,
            ct);
        if (!migrated)
        {
            var current = await _outbox.Find(Builders<BsonDocument>.Filter.Eq("_id", source["_id"]))
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ROW_DISAPPEARED");
            var reinspection = AuditOutboxTemporalStorageCompatibility.Inspect(current);
            if (reinspection.Kind != AuditOutboxTemporalStorageCompatibility.InspectionKind.Current)
            {
                throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_CAS_CONFLICT");
            }
        }
    }

    private async Task<long> CountNotCurrentAsync(CancellationToken ct)
    {
        var documents = await _outbox.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return documents.LongCount(document =>
            AuditOutboxTemporalStorageCompatibility.Inspect(document).Kind
            != AuditOutboxTemporalStorageCompatibility.InspectionKind.Current);
    }

    private async Task<bool> ExactIndexExistsAsync(string indexName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            return false;
        }

        using var cursor = await _outbox.Indexes.ListAsync(ct);
        var indexes = await cursor.ToListAsync(ct);
        var matches = indexes.Where(index =>
                string.Equals(index.GetValue("name", "").AsString, indexName, StringComparison.Ordinal))
            .ToList();
        if (matches.Count != 1)
        {
            return false;
        }

        var index = matches[0];
        var expectedKeys = new BsonDocument
        {
            [nameof(AuditOutboxMessage.Status)] = 1,
            [nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1)] = 1,
            [nameof(AuditOutboxMessage.CreatedAtUtcTicksV1)] = 1,
            ["_id"] = 1
        };
        return index.TryGetValue("key", out var key)
               && key.IsBsonDocument
               && key.AsBsonDocument.Equals(expectedKeys)
               && (!index.TryGetValue("unique", out var unique) || !unique.ToBoolean())
               && (!index.TryGetValue("sparse", out var sparse) || !sparse.ToBoolean())
               && (!index.TryGetValue("hidden", out var hidden) || !hidden.ToBoolean())
               && !index.Contains("partialFilterExpression")
               && !index.Contains("collation");
    }

    private async Task EnsureTransactionCapableReplicaSetAsync(CancellationToken ct)
    {
        BsonDocument hello;
        try
        {
            hello = await _outbox.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("hello", 1),
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_TOPOLOGY_UNVERIFIED", ex);
        }

        if (!hello.TryGetValue("setName", out var setName)
            || !setName.IsString
            || string.IsNullOrWhiteSpace(setName.AsString)
            || !hello.TryGetValue("logicalSessionTimeoutMinutes", out var sessionTimeout)
            || !sessionTimeout.IsNumeric
            || !hello.TryGetValue("isWritablePrimary", out var writablePrimary)
            || !writablePrimary.IsBoolean
            || !writablePrimary.AsBoolean)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_REPLICA_SET_REQUIRED");
        }
    }

    private static Guid ReadGuidId(BsonDocument document)
    {
        var id = document["_id"];
        return id.IsGuid ? id.AsGuid : throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_ID_INVALID");
    }

    private static void ValidateRunFacts(
        string migrationId,
        int targetVersion,
        int batchSize,
        TimeSpan leaseDuration,
        string leaseOwner,
        string selectedIndexName)
    {
        if (!string.Equals(migrationId, AuditOutboxTemporalMigrationState.ExactId, StringComparison.Ordinal)
            || targetVersion != AuditOutboxTemporalMigrationState.ExactTargetVersion
            || batchSize is < 1 or > 1000
            || leaseDuration < TimeSpan.FromSeconds(10)
            || leaseDuration > TimeSpan.FromMinutes(15)
            || string.IsNullOrWhiteSpace(leaseOwner)
            || leaseOwner.Length > 128
            || !string.Equals(selectedIndexName, ScalarClaimIndexName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_RUN_FACTS_INVALID");
        }
    }
}
