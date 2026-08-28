using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class AuditIntentTemporalMigrationRepository : IAuditIntentTemporalMigrationRepository
{
    public const string StateCollectionName = "mdm_audit_intent_temporal_migrations";
    public const int MaximumAggregateDocumentBytes = 1_048_576;
    private const long TicksPerMinute = TimeSpan.TicksPerMinute;
    private const string SelectedIndexEvidence = "tenant-version-state-schedule-timestamp-intent-v1";
    public static readonly string SelectedIndexEvidenceFingerprint = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(SelectedIndexEvidence))).ToLowerInvariant();
    private static readonly CollectionSpec[] Collections =
    [
        new(0, "mdm_code_reservations", "code_reservations", Domain.Enums.AuditAggregateType.CodeReservation),
        new(1, "mdm_global_products", "global_products", Domain.Enums.AuditAggregateType.GlobalProduct),
        new(2, "mdm_product_definition_revisions", "product_definition_revisions", Domain.Enums.AuditAggregateType.ProductDefinitionRevision),
        new(3, "mdm_gskus", "gskus", Domain.Enums.AuditAggregateType.Gsku),
        new(4, "mdm_finished_goods", "finished_goods", Domain.Enums.AuditAggregateType.FinishedGood),
        new(5, "mdm_lskus", "lskus", Domain.Enums.AuditAggregateType.Lsku),
        new(6, "mdm_product_legal_entity_scope_policies", "product_legal_entity_scope_policies", Domain.Enums.AuditAggregateType.ProductLegalEntityScopePolicy),
        new(7, "mdm_product_legal_entity_scope_rollout_states", "product_legal_entity_scope_rollout_states", Domain.Enums.AuditAggregateType.ProductLegalEntityScopeRolloutState)
    ];

    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<AuditIntentTemporalMigrationState> _states;
    private readonly TimeProvider _clock;
    private readonly IAuditIntentTemporalMigrationFailureInjector? _failureInjector;

    public AuditIntentTemporalMigrationRepository(
        IMongoDatabase database,
        TimeProvider clock,
        IAuditIntentTemporalMigrationFailureInjector? failureInjector = null)
    {
        _database = database;
        _states = database.GetCollection<AuditIntentTemporalMigrationState>(StateCollectionName);
        _clock = clock;
        _failureInjector = failureInjector;
    }

    public async Task<AuditIntentTemporalMigrationState?> GetValidatedStateAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.Find(Builders<AuditIntentTemporalMigrationState>.Filter.Empty)
            .Limit(2)
            .ToListAsync(cancellationToken);
        if (states.Count == 0)
        {
            return null;
        }

        if (states.Count != 1 || !string.Equals(states[0].Id, AuditIntentTemporalMigrationState.ExactId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_STATE_AMBIGUOUS");
        }

        states[0].Validate();
        if ((states[0].Phase is AuditIntentTemporalMigrationState.Phases.CompletionVerified
                or AuditIntentTemporalMigrationState.Phases.CutoverActive)
            && !string.Equals(
                states[0].IndexEvidenceFingerprint,
                SelectedIndexEvidenceFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_INDEX_EVIDENCE_INVALID");
        }
        return states[0];
    }

    public async Task<AuditIntentTemporalMigrationResult> RunAsync(
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await RequireReplicaSetAsync(cancellationToken);
        var existing = await GetValidatedStateAsync(cancellationToken);
        if (existing?.Phase == AuditIntentTemporalMigrationState.Phases.CutoverActive)
        {
            if (!string.Equals(
                    existing.IndexEvidenceFingerprint,
                    SelectedIndexEvidenceFingerprint,
                    StringComparison.Ordinal)
                || existing.ActivationVersion != AuditIntentTemporalStorage.CurrentVersion)
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CUTOVER_STATE_INVALID");
            }
            await VerifyAllCurrentAndIndexesAsync(cancellationToken);
            return Result(existing);
        }

        if (existing?.Phase == AuditIntentTemporalMigrationState.Phases.CompletionVerified)
        {
            await VerifyAllCurrentAndIndexesAsync(cancellationToken);
            return Result(request.ActivateCutover
                ? await ActivateAsync(existing, cancellationToken)
                : existing);
        }

        if (existing?.Phase == AuditIntentTemporalMigrationState.Phases.Completed)
        {
            if (!request.VerifyCompletion && !request.ActivateCutover)
            {
                return Result(existing);
            }
            await EnsureScalarIndexesAsync(cancellationToken);
            await VerifyAllCurrentAndIndexesAsync(cancellationToken);
            var verified = await VerifyCompletionAsync(existing, request.SelectedIndexEvidenceFingerprint, cancellationToken);
            return Result(request.ActivateCutover
                ? await ActivateAsync(verified, cancellationToken)
                : verified);
        }

        if (existing is null)
        {
            var boundaries = await PreflightAsync(cancellationToken);
            _failureInjector?.ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint.AfterPreflightBeforeState);
            var nowTicks = _clock.GetUtcNow().UtcTicks;
            var initial = new AuditIntentTemporalMigrationState
            {
                Phase = AuditIntentTemporalMigrationState.Phases.Ready,
                CollectionBoundaries = boundaries,
                UpdatedAtUtcTicks = nowTicks
            };
            await _states.InsertOneAsync(initial, cancellationToken: cancellationToken);
            _failureInjector?.ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint.AfterReadyState);
        }

        var state = await AcquireLeaseAsync(request, cancellationToken);
        try
        {
            while (state.CollectionOrdinal < Collections.Length)
            {
                state = await MigrateBatchAsync(state, request, cancellationToken);
            }

            state = await CompleteAsync(state, cancellationToken);
            _failureInjector?.ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint.AfterCompleted);
            if (request.VerifyCompletion || request.ActivateCutover)
            {
                await EnsureScalarIndexesAsync(cancellationToken);
                await VerifyAllCurrentAndIndexesAsync(cancellationToken);
                state = await VerifyCompletionAsync(state, request.SelectedIndexEvidenceFingerprint, cancellationToken);
            }

            if (request.ActivateCutover)
            {
                state = await ActivateAsync(state, cancellationToken);
            }

            return Result(state);
        }
        catch
        {
            await ReleaseForRecoveryAsync(state, cancellationToken);
            throw;
        }
    }

    private async Task<List<AuditIntentTemporalCollectionBoundary>> PreflightAsync(CancellationToken cancellationToken)
    {
        var boundaries = new List<AuditIntentTemporalCollectionBoundary>(Collections.Length);
        foreach (var spec in Collections)
        {
            var collection = _database.GetCollection<BsonDocument>(spec.Name);
            Guid upper = Guid.Empty;
            using var cursor = await collection.FindAsync(
                new BsonDocument("AuditIntents", new BsonDocument("$exists", true)),
                new FindOptions<BsonDocument> { Sort = new BsonDocument("_id", 1), BatchSize = 100 },
                cancellationToken);
            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var document in cursor.Current)
                {
                    var aggregateId = ValidateAggregate(spec, document);
                    upper = aggregateId;
                }
            }
            boundaries.Add(new AuditIntentTemporalCollectionBoundary
            {
                CollectionOrdinal = spec.Ordinal,
                UpperAggregateId = upper
            });
        }
        return boundaries;
    }

    private async Task<AuditIntentTemporalMigrationState> AcquireLeaseAsync(
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var eligiblePhase = Builders<AuditIntentTemporalMigrationState>.Filter.In(
            state => state.Phase,
            [AuditIntentTemporalMigrationState.Phases.Ready,
             AuditIntentTemporalMigrationState.Phases.RecoveryRequired,
             AuditIntentTemporalMigrationState.Phases.Migrating]);
        var freeLease = Builders<AuditIntentTemporalMigrationState>.Filter.Eq(state => state.LeaseOwner, null)
            | Builders<AuditIntentTemporalMigrationState>.Filter.Lte(state => state.LeaseExpiresAtUtcTicks, now.UtcTicks);
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Set(state => state.Phase, AuditIntentTemporalMigrationState.Phases.Migrating)
            .Set(state => state.LeaseOwner, request.LeaseOwner)
            .Set(state => state.LeaseExpiresAtUtcTicks, now.Add(request.LeaseDuration).UtcTicks)
            .Set(state => state.UpdatedAtUtcTicks, now.UtcTicks)
            .Inc(state => state.LeaseGeneration, 1);
        var state = await _states.FindOneAndUpdateAsync(
            Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Id, AuditIntentTemporalMigrationState.ExactId)
            & eligiblePhase & freeLease,
            update,
            new FindOneAndUpdateOptions<AuditIntentTemporalMigrationState> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return state ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_UNAVAILABLE");
    }

    private async Task<AuditIntentTemporalMigrationState> MigrateBatchAsync(
        AuditIntentTemporalMigrationState state,
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var spec = Collections[state.CollectionOrdinal];
        var boundary = state.CollectionBoundaries.Single(item => item.CollectionOrdinal == spec.Ordinal).UpperAggregateId;
        if (boundary == Guid.Empty)
        {
            return await AdvanceCollectionAsync(state, cancellationToken);
        }

        var collection = _database.GetCollection<BsonDocument>(spec.Name);
        var stages = new List<BsonDocument>
        {
            new("$match", new BsonDocument("_id", new BsonDocument("$lte", new BsonBinaryData(boundary, GuidRepresentation.Standard)))),
            new("$unwind", "$AuditIntents")
        };
        if (state.LastAggregateId.HasValue)
        {
            var lastAggregate = new BsonBinaryData(state.LastAggregateId.Value, GuidRepresentation.Standard);
            var lastIntent = new BsonBinaryData(
                state.LastIntentId ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CURSOR_INVALID"),
                GuidRepresentation.Standard);
            stages.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("_id", new BsonDocument("$gt", lastAggregate)),
                new BsonDocument
                {
                    { "_id", lastAggregate },
                    { "AuditIntents.IntentId", new BsonDocument("$gt", lastIntent) }
                }
            })));
        }
        stages.Add(new BsonDocument("$sort", new BsonDocument { { "_id", 1 }, { "AuditIntents.IntentId", 1 } }));
        stages.Add(new BsonDocument("$limit", request.BatchSize));
        stages.Add(new BsonDocument("$project", new BsonDocument
        {
            { "AggregateId", "$_id" }, { "Intent", "$AuditIntents" }, { "_id", 0 }
        }));
        var selections = await collection.Aggregate<BsonDocument>(stages).ToListAsync(cancellationToken);
        if (selections.Count == 0)
        {
            return await AdvanceCollectionAsync(state, cancellationToken);
        }

        foreach (var selection in selections)
        {
            state = await MigrateIntentAsync(
                spec,
                selection["AggregateId"].AsGuid,
                selection["Intent"].AsBsonDocument,
                state,
                request,
                cancellationToken);
        }
        return state;
    }

    private async Task<AuditIntentTemporalMigrationState> MigrateIntentAsync(
        CollectionSpec spec,
        Guid aggregateId,
        BsonDocument sourceIntent,
        AuditIntentTemporalMigrationState state,
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var intentId = ReadGuid(sourceIntent, "IntentId");
        var inspection = Inspect(sourceIntent);
        var now = _clock.GetUtcNow();
        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var leaseFilter = LeaseFilter(state, request.LeaseOwner, now.UtcTicks);
            var lease = await _states.Find(session, leaseFilter).SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_LOST");
            var collection = _database.GetCollection<BsonDocument>(spec.Name);
            var source = await collection.Find(session, Builders<BsonDocument>.Filter.Eq("_id", aggregateId))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_AGGREGATE_CAS_CONFLICT");
            _ = ValidateAggregate(spec, source);
            if (inspection.IsLegacy)
            {
                var migratedIntents = BuildMigratedIntentArray(ReadIntents(source), intentId, sourceIntent);
                var sizeCandidate = source.DeepClone().AsBsonDocument;
                sizeCandidate["AuditIntents"] = migratedIntents;
                if (spec.AggregateType is Domain.Enums.AuditAggregateType.ProductLegalEntityScopePolicy
                        or Domain.Enums.AuditAggregateType.ProductLegalEntityScopeRolloutState
                    && sizeCandidate.ToBson().Length > MaximumAggregateDocumentBytes)
                {
                    throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_DOCUMENT_BUDGET_EXCEEDED");
                }

                var mapped = BuildMigratedIntentArrayExpression(sourceIntent);
                var updateFilter = new BsonDocument
                {
                    { "_id", source["_id"] },
                    { "TenantId", source["TenantId"] },
                    { "AuditIntents", sourceIntent }
                };
                var update = new PipelineUpdateDefinition<BsonDocument>(
                    PipelineDefinition<BsonDocument, BsonDocument>.Create(
                    [
                        new BsonDocument("$set", new BsonDocument("AuditIntents", mapped))
                    ]));
                var result = await collection.UpdateOneAsync(session, updateFilter, update, cancellationToken: cancellationToken);
                if (result.ModifiedCount != 1)
                {
                    throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_AGGREGATE_CAS_CONFLICT");
                }
                _failureInjector?.ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint.AfterAggregateBeforeCheckpoint);
            }

            var stateUpdate = Builders<AuditIntentTemporalMigrationState>.Update
                .Set(item => item.LastAggregateId, aggregateId)
                .Set(item => item.LastIntentId, intentId)
                .Set(item => item.UpdatedAtUtcTicks, now.UtcTicks)
                .Set(item => item.LeaseExpiresAtUtcTicks, now.Add(request.LeaseDuration).UtcTicks)
                .Inc(item => item.ScannedCount, 1)
                .Inc(item => item.MigratedCount, inspection.IsLegacy ? 1 : 0)
                .Inc(item => item.AlreadyCurrentCount, inspection.IsLegacy ? 0 : 1);
            var stateResult = await _states.UpdateOneAsync(session, leaseFilter, stateUpdate, cancellationToken: cancellationToken);
            if (stateResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_LOST");
            }
            await session.CommitTransactionAsync(cancellationToken);
            _failureInjector?.ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint.AfterCheckpointCommit);
            lease.LastAggregateId = aggregateId;
            lease.LastIntentId = intentId;
            lease.ScannedCount++;
            lease.MigratedCount += inspection.IsLegacy ? 1 : 0;
            lease.AlreadyCurrentCount += inspection.IsLegacy ? 0 : 1;
            lease.UpdatedAtUtcTicks = now.UtcTicks;
            lease.LeaseExpiresAtUtcTicks = now.Add(request.LeaseDuration).UtcTicks;
            return lease;
        }
        catch
        {
            await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<AuditIntentTemporalMigrationState> AdvanceCollectionAsync(
        AuditIntentTemporalMigrationState state,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcTicks;
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Inc(item => item.CollectionOrdinal, 1)
            .Set(item => item.LastAggregateId, null)
            .Set(item => item.LastIntentId, null)
            .Set(item => item.UpdatedAtUtcTicks, now);
        return await _states.FindOneAndUpdateAsync(
            LeaseFilter(state, state.LeaseOwner!, now),
            update,
            new FindOneAndUpdateOptions<AuditIntentTemporalMigrationState> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_LOST");
    }

    private async Task<AuditIntentTemporalMigrationState> CompleteAsync(
        AuditIntentTemporalMigrationState state,
        CancellationToken cancellationToken)
    {
        if (state.Phase is AuditIntentTemporalMigrationState.Phases.Completed
            or AuditIntentTemporalMigrationState.Phases.CompletionVerified
            or AuditIntentTemporalMigrationState.Phases.CutoverActive)
        {
            return state;
        }
        var now = _clock.GetUtcNow().UtcTicks;
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Set(item => item.Phase, AuditIntentTemporalMigrationState.Phases.Completed)
            .Set(item => item.CompletedAtUtcTicks, now)
            .Set(item => item.LeaseOwner, null)
            .Set(item => item.LeaseExpiresAtUtcTicks, null)
            .Set(item => item.UpdatedAtUtcTicks, now);
        return await _states.FindOneAndUpdateAsync(
            LeaseFilter(state, state.LeaseOwner!, now), update,
            new FindOneAndUpdateOptions<AuditIntentTemporalMigrationState> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_LEASE_LOST");
    }

    private async Task<AuditIntentTemporalMigrationState> VerifyCompletionAsync(
        AuditIntentTemporalMigrationState state,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcTicks;
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Set(item => item.Phase, AuditIntentTemporalMigrationState.Phases.CompletionVerified)
            .Set(item => item.IndexEvidenceFingerprint, fingerprint)
            .Set(item => item.UpdatedAtUtcTicks, now);
        return await _states.FindOneAndUpdateAsync(
            Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Id, state.Id)
            & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Phase, AuditIntentTemporalMigrationState.Phases.Completed),
            update,
            new FindOneAndUpdateOptions<AuditIntentTemporalMigrationState> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_COMPLETION_VERIFY_CONFLICT");
    }

    private async Task<AuditIntentTemporalMigrationState> ActivateAsync(
        AuditIntentTemporalMigrationState state,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcTicks;
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Set(item => item.Phase, AuditIntentTemporalMigrationState.Phases.CutoverActive)
            .Set(item => item.ActivationVersion, AuditIntentTemporalStorage.CurrentVersion)
            .Set(item => item.UpdatedAtUtcTicks, now);
        return await _states.FindOneAndUpdateAsync(
            Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Id, state.Id)
            & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Phase, AuditIntentTemporalMigrationState.Phases.CompletionVerified)
            & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(
                item => item.IndexEvidenceFingerprint,
                SelectedIndexEvidenceFingerprint),
            update,
            new FindOneAndUpdateOptions<AuditIntentTemporalMigrationState> { ReturnDocument = ReturnDocument.After },
            cancellationToken) ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_ACTIVATION_CONFLICT");
    }

    private async Task ReleaseForRecoveryAsync(AuditIntentTemporalMigrationState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.LeaseOwner)) return;
        var update = Builders<AuditIntentTemporalMigrationState>.Update
            .Set(item => item.Phase, AuditIntentTemporalMigrationState.Phases.RecoveryRequired)
            .Set(item => item.LeaseOwner, null)
            .Set(item => item.LeaseExpiresAtUtcTicks, null)
            .Set(item => item.UpdatedAtUtcTicks, _clock.GetUtcNow().UtcTicks);
        await _states.UpdateOneAsync(
            Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Id, state.Id)
            & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.LeaseGeneration, state.LeaseGeneration)
            & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.LeaseOwner, state.LeaseOwner),
            update,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureScalarIndexesAsync(CancellationToken cancellationToken)
    {
        foreach (var spec in Collections)
        {
            var collection = _database.GetCollection<BsonDocument>(spec.Name);
            try
            {
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(ExpectedIndexKeys, new CreateIndexOptions { Name = IndexName(spec) }),
                    cancellationToken: cancellationToken);
            }
            catch (MongoCommandException exception) when (
                exception.CodeName is "IndexKeySpecsConflict" or "IndexOptionsConflict")
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_INDEX_INVALID", exception);
            }
        }
    }

    private async Task VerifyAllCurrentAndIndexesAsync(CancellationToken cancellationToken)
    {
        foreach (var spec in Collections)
        {
            var collection = _database.GetCollection<BsonDocument>(spec.Name);
            using var cursor = await collection.FindAsync(
                new BsonDocument("AuditIntents", new BsonDocument("$exists", true)),
                cancellationToken: cancellationToken);
            while (await cursor.MoveNextAsync(cancellationToken))
            foreach (var document in cursor.Current)
            {
                _ = ValidateAggregate(spec, document);
                foreach (var intent in ReadIntents(document))
                    if (Inspect(intent).IsLegacy)
                        throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_LEGACY_REMAINS");
            }

            var indexes = await (await collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
            var named = indexes.Where(index => index["name"] == IndexName(spec)).Take(2).ToArray();
            if (named.Length != 1
                || !named[0]["key"].AsBsonDocument.Equals(ExpectedIndexKeys)
                || named[0].TryGetValue("unique", out var unique) && unique.ToBoolean()
                || named[0].TryGetValue("sparse", out var sparse) && sparse.ToBoolean()
                || named[0].Contains("partialFilterExpression"))
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_INDEX_INVALID");
        }
    }

    private async Task RequireReplicaSetAsync(CancellationToken cancellationToken)
    {
        var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        if (!hello.TryGetValue("setName", out var setName) || !setName.IsString || string.IsNullOrWhiteSpace(setName.AsString))
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_REPLICA_SET_REQUIRED");
    }

    internal static AuditIntentTemporalStorageKind ValidateRawIntent(BsonDocument intent)
        => Inspect(intent).IsLegacy
            ? AuditIntentTemporalStorageKind.Legacy
            : AuditIntentTemporalStorageKind.Current;

    private static Inspection Inspect(BsonDocument intent)
    {
        var timestamp = ReadLegacyTicks(intent, "TimestampUtc", required: true)!.Value;
        var next = ReadLegacyTicks(intent, "NextRetryAt", required: false);
        var lease = ReadLegacyTicks(intent, "LeaseUntil", required: false);
        var hasTimestamp = intent.Contains("TimestampUtcTicksV1");
        var hasNext = intent.Contains("NextRetryAtUtcTicksV1");
        var hasLease = intent.Contains("LeaseUntilUtcTicksV1");
        var hasVersion = intent.Contains("TemporalStorageVersion");
        if (!hasTimestamp && !hasNext && !hasLease && !hasVersion) return new(true, timestamp, next, lease);
        if (!hasTimestamp || !hasNext || !hasLease || !hasVersion
            || !intent["TimestampUtcTicksV1"].IsInt64 || !intent["TemporalStorageVersion"].IsInt32
            || intent["TemporalStorageVersion"].AsInt32 != AuditIntentTemporalStorage.CurrentVersion
            || ReadNullableShadow(intent["NextRetryAtUtcTicksV1"]) != next
            || ReadNullableShadow(intent["LeaseUntilUtcTicksV1"]) != lease
            || intent["TimestampUtcTicksV1"].AsInt64 != timestamp)
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SHAPE_INVALID");
        return new(false, timestamp, next, lease);
    }

    private static long? ReadLegacyTicks(BsonDocument document, string name, bool required)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            if (required) throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_LEGACY_SHAPE_INVALID");
            return null;
        }
        if (!value.IsBsonArray || value.AsBsonArray.Count != 2 || !value[0].IsInt64 || !value[1].IsInt32)
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_LEGACY_SHAPE_INVALID");
        var offset = value[1].AsInt32;
        if (offset is < -840 or > 840) throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_OFFSET_INVALID");
        return checked(value[0].AsInt64 - offset * TicksPerMinute);
    }

    private static long? ReadNullableShadow(BsonValue value)
        => value.IsBsonNull ? null : value.IsInt64 ? value.AsInt64 : throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SHADOW_INVALID");

    private static BsonValue BuildMigratedIntentArrayExpression(BsonDocument sourceIntent) => new BsonDocument("$map", new BsonDocument
    {
        { "input", "$AuditIntents" }, { "as", "intent" },
        { "in", new BsonDocument("$cond", new BsonArray
            {
                new BsonDocument("$eq", new BsonArray
                {
                    "$$intent",
                    new BsonDocument("$literal", sourceIntent)
                }),
                new BsonDocument("$mergeObjects", new BsonArray { "$$intent", new BsonDocument
                    {
                        { "TimestampUtcTicksV1", UtcTicksExpression("$$intent.TimestampUtc") },
                        { "NextRetryAtUtcTicksV1", NullableUtcTicksExpression("$$intent.NextRetryAt") },
                        { "LeaseUntilUtcTicksV1", NullableUtcTicksExpression("$$intent.LeaseUntil") },
                        { "TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion }
                    }}),
                "$$intent"
            })}
    });

    private static BsonArray BuildMigratedIntentArray(
        IReadOnlyList<BsonDocument> intents,
        Guid intentId,
        BsonDocument expectedSource)
    {
        var result = new BsonArray(intents.Count);
        var matches = 0;
        foreach (var source in intents)
        {
            var inspection = Inspect(source);
            var target = source.DeepClone().AsBsonDocument;
            if (ReadGuid(source, "IntentId") == intentId && source.Equals(expectedSource))
            {
                matches++;
                if (!inspection.IsLegacy)
                {
                    throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_AGGREGATE_CAS_CONFLICT");
                }
                target["TimestampUtcTicksV1"] = inspection.TimestampTicks;
                target["NextRetryAtUtcTicksV1"] = inspection.NextTicks.HasValue
                    ? inspection.NextTicks.Value
                    : BsonNull.Value;
                target["LeaseUntilUtcTicksV1"] = inspection.LeaseTicks.HasValue
                    ? inspection.LeaseTicks.Value
                    : BsonNull.Value;
                target["TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion;
            }
            result.Add(target);
        }
        if (matches != 1)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_AGGREGATE_CAS_CONFLICT");
        }
        return result;
    }

    private static BsonValue UtcTicksExpression(string path) => new BsonDocument("$subtract", new BsonArray
    {
        new BsonDocument("$arrayElemAt", new BsonArray { path, 0 }),
        new BsonDocument("$multiply", new BsonArray { new BsonDocument("$arrayElemAt", new BsonArray { path, 1 }), TicksPerMinute })
    });

    private static BsonValue NullableUtcTicksExpression(string path) => new BsonDocument("$cond", new BsonArray
    {
        new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", path), "array" }),
        UtcTicksExpression(path), BsonNull.Value
    });

    private static IReadOnlyList<BsonDocument> ReadIntents(BsonDocument document)
    {
        if (!document.TryGetValue("AuditIntents", out var value))
        {
            return [];
        }

        if (!value.IsBsonArray || value.AsBsonArray.Any(item => !item.IsBsonDocument))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CONTAINER_INVALID");
        }

        return value.AsBsonArray.Select(item => item.AsBsonDocument).ToArray();
    }

    private static Guid ReadGuid(BsonDocument document, string name)
        => document.TryGetValue(name, out var value) && value.IsGuid ? value.AsGuid
            : throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_GUID_INVALID");

    private static Guid ValidateAggregate(CollectionSpec spec, BsonDocument document)
    {
        var aggregateId = ReadGuid(document, "_id");
        var tenantId = ReadGuid(document, "TenantId");
        var seen = new HashSet<Guid>();
        foreach (var intent in ReadIntents(document))
        {
            var intentId = ReadGuid(intent, "IntentId");
            if (!seen.Add(intentId)
                || ReadGuid(intent, "TenantId") != tenantId
                || ReadGuid(intent, "AggregateId") != aggregateId
                || !intent.TryGetValue("AggregateType", out var aggregateType)
                || !aggregateType.IsInt32
                || aggregateType.AsInt32 != (int)spec.AggregateType
                || !intent.TryGetValue("SourceService", out var sourceService)
                || !sourceService.IsString
                || !string.Equals(sourceService.AsString, AuditIntentContract.SourceService, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_BINDING_INVALID");
            }
            Inspect(intent);
        }
        return aggregateId;
    }

    private static FilterDefinition<AuditIntentTemporalMigrationState> LeaseFilter(
        AuditIntentTemporalMigrationState state, string owner, long nowTicks)
        => Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.Id, state.Id)
           & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.LeaseGeneration, state.LeaseGeneration)
           & Builders<AuditIntentTemporalMigrationState>.Filter.Eq(item => item.LeaseOwner, owner)
           & Builders<AuditIntentTemporalMigrationState>.Filter.Gt(item => item.LeaseExpiresAtUtcTicks, nowTicks);

    private static void ValidateRequest(AuditIntentTemporalMigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LeaseOwner) || request.LeaseOwner.Length > 128
            || request.BatchSize is < 1 or > 1000
            || request.LeaseDuration < TimeSpan.FromSeconds(10) || request.LeaseDuration > TimeSpan.FromMinutes(15)
            || (request.VerifyCompletion || request.ActivateCutover)
               && !string.Equals(
                   request.SelectedIndexEvidenceFingerprint,
                   SelectedIndexEvidenceFingerprint,
                   StringComparison.Ordinal))
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_MIGRATION_REQUEST_INVALID");
    }

    private static BsonDocument ExpectedIndexKeys => new()
    {
        { "TenantId", 1 },
        { "AuditIntents.TemporalStorageVersion", 1 },
        { "AuditIntents.DeliveryState", 1 },
        { "AuditIntents.NextRetryAtUtcTicksV1", 1 },
        { "AuditIntents.LeaseUntilUtcTicksV1", 1 },
        { "AuditIntents.TimestampUtcTicksV1", 1 },
        { "AuditIntents.IntentId", 1 }
    };

    private static string IndexName(CollectionSpec spec) => $"ix_mdm_{spec.IndexSuffix}_{AuditIntentTemporalMigrationState.ExactIndexSuffix}";
    private static AuditIntentTemporalMigrationResult Result(AuditIntentTemporalMigrationState state)
        => new(state.Phase, state.ScannedCount, state.MigratedCount, state.AlreadyCurrentCount,
            state.CollectionOrdinal, state.LastAggregateId, state.LastIntentId);
    private sealed record CollectionSpec(
        int Ordinal,
        string Name,
        string IndexSuffix,
        Domain.Enums.AuditAggregateType AggregateType);
    private sealed record Inspection(bool IsLegacy, long TimestampTicks, long? NextTicks, long? LeaseTicks);
}

public enum AuditIntentTemporalMigrationFailurePoint
{
    AfterPreflightBeforeState,
    AfterReadyState,
    AfterAggregateBeforeCheckpoint,
    AfterCheckpointCommit,
    AfterCompleted
}

public interface IAuditIntentTemporalMigrationFailureInjector
{
    void ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint point);
}
