using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditOutboxTemporalStorageMigrationMongoTests(
    AuditOutboxTemporalReplicaSetFixture replicaSet) :
    IAsyncLifetime,
    IClassFixture<AuditOutboxTemporalReplicaSetFixture>
{
    private const string ScalarClaimIndex = "ix_audit_outbox_status_next_attempt_ticks_v1_created_ticks_v1_id";

    private IMongoClient _client = null!;
    private IMongoDatabase _database = null!;
    private IMongoCollection<BsonDocument> _raw = null!;
    private AuditOutboxRepository _repository = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _databaseName = $"fu02rs_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(replicaSet.ConnectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        _client = new MongoClient(settings);
        var hello = await _client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
        Assert.True(hello.TryGetValue("setName", out var setName) && setName.IsString);
        Assert.True(hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean());

        _database = _client.GetDatabase(_databaseName);
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { SchemaProfile.AccessGovernance });
        _raw = _database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        _repository = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            new AuditOutboxTemporalMigrationRepository(_database));
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task TryEnqueueAsync_NewMessage_AtomicallyPersistsLegacyAndScalarRepresentations()
    {
        var request = Request();

        var inserted = await _repository.TryEnqueueAsync(request);
        var document = await _raw.Find(new BsonDocument("IdempotencyKey", request.IdempotencyKey)).SingleAsync();

        Assert.True(inserted);
        Assert.Equal(BsonType.Array, document["NextAttemptAtUtc"].BsonType);
        Assert.Equal(BsonType.Array, document["CreatedAtUtc"].BsonType);
        Assert.Equal(BsonType.Int64, document["NextAttemptAtUtcTicksV1"].BsonType);
        Assert.Equal(BsonType.Int64, document["CreatedAtUtcTicksV1"].BsonType);
        Assert.Equal(1, document["TemporalStorageVersion"].AsInt32);
        Assert.Equal(
            AuditOutboxTemporalStorageCompatibility.InspectionKind.Current,
            AuditOutboxTemporalStorageCompatibility.Inspect(document).Kind);
    }

    [Fact]
    public async Task ClaimAndRetry_CurrentV1Message_KeepLegacyAndShadowInExactAgreement()
    {
        var request = Request();
        await _repository.TryEnqueueAsync(request);
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        var claimed = Assert.Single(await _repository.ClaimNextBatchAsync(
            batchSize: 1,
            maxAttempts: 5,
            now,
            processingStaleAfter: TimeSpan.FromMinutes(5)));
        var afterClaim = await RawByIdAsync(claimed.Id);
        var claimInspection = AuditOutboxTemporalStorageCompatibility.Inspect(afterClaim);

        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Current, claimInspection.Kind);
        Assert.Equal(now.UtcTicks, claimInspection.NextAttemptAtUtcTicks);

        var retryAt = now.AddMinutes(3).ToOffset(TimeSpan.FromHours(11));
        await _repository.MarkFailedAsync(
            claimed.Id,
            AuditOutboxStatus.Failed,
            attempts: 1,
            retryAt,
            lastError: "bounded-test-error");

        var afterRetry = await RawByIdAsync(claimed.Id);
        var retryInspection = AuditOutboxTemporalStorageCompatibility.Inspect(afterRetry);
        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Current, retryInspection.Kind);
        Assert.Equal(retryAt.UtcTicks, retryInspection.NextAttemptAtUtcTicks);
        Assert.Equal(retryAt.UtcTicks, afterRetry["NextAttemptAtUtcTicksV1"].AsInt64);
    }

    [Fact]
    public async Task ExistingLegacyOnlyMessage_RollbackCompatibleReadDoesNotMaterializeHalfShadow()
    {
        var id = Guid.NewGuid();
        var next = DateTimeOffset.UtcNow.AddMinutes(-1).ToOffset(TimeSpan.FromHours(14));
        var created = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(-12));
        var document = RawLegacy(id, next, created);
        await _raw.InsertOneAsync(document);

        var readBack = await RawByIdAsync(id);
        var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(readBack);

        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Legacy, inspection.Kind);
        Assert.False(readBack.Contains("NextAttemptAtUtcTicksV1"));
        Assert.False(readBack.Contains("CreatedAtUtcTicksV1"));
        Assert.False(readBack.Contains("TemporalStorageVersion"));
    }

    [Fact]
    public async Task ConcurrentClaims_CurrentV1Message_IsReturnedExactlyOnce()
    {
        var request = Request();
        await _repository.TryEnqueueAsync(request);
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        var claims = await Task.WhenAll(
            _repository.ClaimNextBatchAsync(1, 5, now, TimeSpan.FromMinutes(5)),
            _repository.ClaimNextBatchAsync(1, 5, now, TimeSpan.FromMinutes(5)));

        Assert.Single(claims.SelectMany(batch => batch));
    }

    [Fact]
    public async Task StandaloneTopology_PreflightRejectsBeforeMutation_WhileNormalOutboxRemainsOperational()
    {
        var databaseName = $"fu02standalone_{Guid.NewGuid():N}";
        var settings = MongoClientSettings.FromConnectionString(replicaSet.StandaloneConnectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var database = client.GetDatabase(databaseName);

        try
        {
            await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.AccessGovernance });
            var raw = database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
            var state = database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutboxTemporalMigrations);
            var legacy = RawLegacy(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow.AddHours(-1));
            await raw.InsertOneAsync(legacy);
            var before = await raw.Find(new BsonDocument("_id", legacy["_id"])).SingleAsync();
            var stateRepository = new AuditOutboxTemporalMigrationRepository(database);
            var runner = new AuditOutboxTemporalStorageMigrationRunner(database, stateRepository, TimeProvider.System);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.PreflightAsync());

            Assert.Equal("AUDIT_OUTBOX_TEMPORAL_REPLICA_SET_REQUIRED", error.Message);
            Assert.Equal(0, await state.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(before, await raw.Find(new BsonDocument("_id", legacy["_id"])).SingleAsync());

            var normalRepository = new AuditOutboxRepository(
                new PlatformDbContext(client, database),
                stateRepository);
            var request = Request();
            Assert.True(await normalRepository.TryEnqueueAsync(request));
            var current = await raw.Find(new BsonDocument("IdempotencyKey", request.IdempotencyKey)).SingleAsync();
            Assert.Equal(
                AuditOutboxTemporalStorageCompatibility.InspectionKind.Current,
                AuditOutboxTemporalStorageCompatibility.Inspect(current).Kind);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task Preflight_MalformedHalfShadow_FailsBeforeStateOrRowMutation()
    {
        var id = Guid.NewGuid();
        var original = RawLegacy(id, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(-1));
        original["NextAttemptAtUtcTicksV1"] = DateTimeOffset.UtcNow.UtcTicks;
        await _raw.InsertOneAsync(original);
        var before = await RawByIdAsync(id);
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var runner = new AuditOutboxTemporalStorageMigrationRunner(
            _database,
            stateRepository,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.PreflightAsync());

        Assert.StartsWith("AUDIT_OUTBOX_TEMPORAL_", error.Message, StringComparison.Ordinal);
        var failedState = Assert.IsType<AuditOutboxTemporalMigrationState>(await stateRepository.GetAsync());
        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.Failed, failedState.Phase);
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE", failedState.FailureCode);
        Assert.Equal(before, await RawByIdAsync(id));
    }

    [Fact]
    public async Task Run_NonGuidId_FailsBeforeAnyOutboxRowMutation()
    {
        var malformed = RawLegacy(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(-1));
        malformed["_id"] = "not-a-guid";
        await _raw.InsertOneAsync(malformed);
        var before = await _raw.Find(new BsonDocument("_id", "not-a-guid")).SingleAsync();
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync(Runner(stateRepository), activate: false));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_ID_INVALID", error.Message);
        Assert.Equal(before, await _raw.Find(new BsonDocument("_id", "not-a-guid")).SingleAsync());
    }

    [Fact]
    public async Task UnexpectedMigrationStateDocument_BlocksAllStateReads()
    {
        var rawState = _database.GetCollection<BsonDocument>(
            AuditCollectionNames.AuditOutboxTemporalMigrations);
        await rawState.InsertOneAsync(new BsonDocument("_id", "unexpected-state"));
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => stateRepository.GetAsync());

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_STATE_INVENTORY_INVALID", error.Message);
    }

    [Fact]
    public async Task LeaseGeneration_FencesContenderAndStaleOwner()
    {
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        const long now = 638920656000000000L;
        await stateRepository.BeginPreflightAsync(now);
        await stateRepository.CompletePreflightAsync(2, 0, new string('a', 64), now);
        var first = await stateRepository.AcquireLeaseAsync("owner-a", now, TimeSpan.FromMinutes(1).Ticks);

        var contention = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stateRepository.AcquireLeaseAsync("owner-b", now + 1, TimeSpan.FromMinutes(1).Ticks));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_LEASE_UNAVAILABLE", contention.Message);

        var second = await stateRepository.AcquireLeaseAsync(
            "owner-b",
            now + TimeSpan.FromMinutes(1).Ticks + 1,
            TimeSpan.FromMinutes(1).Ticks);
        Assert.Equal(first.LeaseGeneration + 1, second.LeaseGeneration);

        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stateRepository.SaveCheckpointAsync(
                "owner-a",
                first.LeaseGeneration,
                Guid.NewGuid(),
                1,
                0,
                now + TimeSpan.FromMinutes(1).Ticks + 2,
                TimeSpan.FromMinutes(1).Ticks));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_LEASE_LOST", stale.Message);
    }

    [Fact]
    public async Task SameOwnerConcurrentLeaseAttempt_IsRejected()
    {
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        const long now = 638920656000000000L;
        await stateRepository.BeginPreflightAsync(now);
        await stateRepository.CompletePreflightAsync(1, 0, new string('e', 64), now);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await stateRepository.AcquireLeaseAsync("same-owner", now, TimeSpan.FromMinutes(1).Ticks);
                return "acquired";
            }
            catch (InvalidOperationException error)
            {
                return error.Message;
            }
        }));

        Assert.Single(attempts, result => result == "acquired");
        Assert.Single(attempts, result => result == "AUDIT_OUTBOX_TEMPORAL_LEASE_UNAVAILABLE");
    }

    [Fact]
    public async Task ExpiredLease_CannotCompleteOrVerify()
    {
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        const long now = 638920656000000000L;
        var duration = TimeSpan.FromMinutes(1).Ticks;
        await stateRepository.BeginPreflightAsync(now);
        await stateRepository.CompletePreflightAsync(1, 0, new string('f', 64), now);
        var lease = await stateRepository.AcquireLeaseAsync("expired-owner", now, duration);
        var expiredAt = now + duration + 1;

        var completeError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stateRepository.SetFinalCountsAndCompletedAsync(
                "expired-owner",
                lease.LeaseGeneration,
                1,
                expiredAt));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_LEASE_LOST", completeError.Message);

        var rawState = _database.GetCollection<BsonDocument>(
            AuditCollectionNames.AuditOutboxTemporalMigrations);
        await rawState.UpdateOneAsync(
            new BsonDocument("_id", AuditOutboxTemporalMigrationState.ExactId),
            new BsonDocument("$set", new BsonDocument("Phase", AuditOutboxTemporalMigrationState.Phases.Completed)));
        var verifyError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stateRepository.VerifyCompletionAndReleaseAsync(
                "expired-owner",
                lease.LeaseGeneration,
                ScalarClaimIndex,
                expiredAt));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_COMPLETION_NOT_VERIFIED", verifyError.Message);
    }

    [Fact]
    public async Task Worker_WithDurableStateBeforeActivation_IsFailClosed()
    {
        var request = Request();
        await _repository.TryEnqueueAsync(request);
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        await stateRepository.BeginPreflightAsync(DateTimeOffset.UtcNow.UtcTicks);
        await stateRepository.CompletePreflightAsync(
            1,
            1,
            new string('b', 64),
            DateTimeOffset.UtcNow.UtcTicks);
        var fencedRepository = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            stateRepository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fencedRepository.ClaimNextBatchAsync(
                1,
                5,
                DateTimeOffset.UtcNow.AddMinutes(1),
                TimeSpan.FromMinutes(5)));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_WORKER_FENCED", error.Message);
        var document = await _raw.Find(new BsonDocument("IdempotencyKey", request.IdempotencyKey)).SingleAsync();
        Assert.Equal((int)AuditOutboxStatus.Pending, document["Status"].AsInt32);
        Assert.Equal(0, document["Attempts"].AsInt32);
    }

    [Theory]
    [InlineData(2, ScalarClaimIndex, false)]
    [InlineData(1, "ix_wrong", false)]
    [InlineData(1, ScalarClaimIndex, true)]
    public async Task Worker_InvalidCutoverState_IsFailClosed(
        int activationVersion,
        string selectedIndex,
        bool hasLease)
    {
        var request = Request();
        await _repository.TryEnqueueAsync(request);
        var stateCollection = _database.GetCollection<AuditOutboxTemporalMigrationState>(
            AuditCollectionNames.AuditOutboxTemporalMigrations);
        await stateCollection.InsertOneAsync(new AuditOutboxTemporalMigrationState
        {
            Phase = AuditOutboxTemporalMigrationState.Phases.CutoverActive,
            ScannedCount = 1,
            AlreadyCurrentCount = 1,
            SourceFingerprint = new string('c', 64),
            StartedAtUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            ActivationVersion = activationVersion,
            SelectedIndexName = selectedIndex,
            LeaseOwner = hasLease ? "unexpected-owner" : null,
            LeaseGeneration = hasLease ? 1 : 0,
            LeaseExpiresAtUtcTicks = hasLease ? DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks : null
        });
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var guarded = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            stateRepository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.ClaimNextBatchAsync(
            1,
            5,
            DateTimeOffset.UtcNow.AddMinutes(1),
            TimeSpan.FromMinutes(5)));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_ACTIVE_STATE_INVALID", error.Message);
        var persisted = await _raw.Find(new BsonDocument("IdempotencyKey", request.IdempotencyKey)).SingleAsync();
        Assert.Equal((int)AuditOutboxStatus.Pending, persisted["Status"].AsInt32);
    }

    [Fact]
    public async Task Worker_MismatchedLegacyAndShadowRow_IsNotClaimed()
    {
        var id = Guid.NewGuid();
        var next = DateTimeOffset.UtcNow.AddMinutes(-1);
        var malformed = RawLegacy(id, next, DateTimeOffset.UtcNow.AddHours(-1));
        malformed["NextAttemptAtUtcTicksV1"] = next.UtcTicks + 1;
        malformed["CreatedAtUtcTicksV1"] = malformed["CreatedAtUtc"].AsBsonArray[0].AsInt64;
        malformed["TemporalStorageVersion"] = 1;
        await _raw.InsertOneAsync(malformed);
        var stateRepository = await InsertActiveStateAsync(scannedCount: 1);
        var guarded = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            stateRepository);

        var claimed = await guarded.ClaimNextBatchAsync(
            1,
            5,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));

        Assert.Empty(claimed);
        Assert.Equal(malformed, await RawByIdAsync(id));
    }

    [Fact]
    public async Task MigrationRunner_BoundedCasMigration_ActivatesOnlyAfterVerifiedZeroLoss()
    {
        var rows = Enumerable.Range(0, 5)
            .Select(index => RawLegacy(
                GuidFromOrdinal(index + 1),
                DateTimeOffset.UtcNow.AddMinutes(-index - 1).ToOffset(OffsetFor(index)),
                DateTimeOffset.UtcNow.AddHours(-index - 1).ToOffset(OffsetFor(index + 1))))
            .ToArray();
        await _raw.InsertManyAsync(rows);
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var runner = new AuditOutboxTemporalStorageMigrationRunner(
            _database,
            stateRepository,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)));

        var completed = await runner.RunAsync(
            AuditOutboxTemporalMigrationState.ExactId,
            AuditOutboxTemporalMigrationState.ExactTargetVersion,
            batchSize: 2,
            leaseDuration: TimeSpan.FromMinutes(1),
            leaseOwner: "bounded-cas",
            activateScalarClaims: true,
            selectedIndexName: ScalarClaimIndex);

        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.CutoverActive, completed.Phase);
        Assert.Equal(5, completed.ScannedCount);
        Assert.Equal(5, completed.MigratedCount);
        Assert.Equal(0, completed.AlreadyCurrentCount);
        Assert.Null(completed.LeaseOwner);
        Assert.Equal(ScalarClaimIndex, completed.SelectedIndexName);
        var persisted = await _raw.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.All(persisted, row => Assert.Equal(
            AuditOutboxTemporalStorageCompatibility.InspectionKind.Current,
            AuditOutboxTemporalStorageCompatibility.Inspect(row).Kind));

        var fencedRepository = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            stateRepository);
        var claimed = await fencedRepository.ClaimNextBatchAsync(
            5,
            5,
            DateTimeOffset.UtcNow.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        Assert.Equal(5, claimed.Count);
    }

    [Fact]
    public async Task MigrationRunner_DualWrittenInsertDuringMigration_IsPreservedAndDoesNotBlockCompletion()
    {
        await _raw.InsertManyAsync(Enumerable.Range(0, 3)
            .Select(index => RawLegacy(
                GuidFromOrdinal(index + 40),
                DateTimeOffset.UtcNow.AddMinutes(-index - 1),
                DateTimeOffset.UtcNow.AddHours(-index - 1))));
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var runner = Runner(stateRepository);
        var inserted = false;
        runner.FailureInjector = async (point, ct) =>
        {
            if (!inserted && point == AuditOutboxTemporalStorageMigrationRunner.BeforeFirstBatch)
            {
                inserted = true;
                Assert.True(await _repository.TryEnqueueAsync(Request(), ct));
            }
        };

        var completed = await RunAsync(runner, activate: false);

        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.CompletionVerified, completed.Phase);
        var rows = await _raw.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(
            AuditOutboxTemporalStorageCompatibility.InspectionKind.Current,
            AuditOutboxTemporalStorageCompatibility.Inspect(row).Kind));
    }

    [Fact]
    public async Task MigrationRunner_DualWrittenInsertAfterVerification_DelayedActivationSucceeds()
    {
        await _raw.InsertManyAsync(Enumerable.Range(0, 2)
            .Select(index => RawLegacy(
                GuidFromOrdinal(index + 50),
                DateTimeOffset.UtcNow.AddMinutes(-index - 1),
                DateTimeOffset.UtcNow.AddHours(-index - 1))));
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var first = await RunAsync(Runner(stateRepository), activate: false);
        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.CompletionVerified, first.Phase);
        Assert.True(await _repository.TryEnqueueAsync(Request()));

        var activated = await RunAsync(Runner(stateRepository), activate: true);

        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.CutoverActive, activated.Phase);
        Assert.Equal(3, await _raw.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task MigrationRunner_WrongSameNameIndexDefinition_FailsBeforeStateOrOutboxMutation()
    {
        await _raw.InsertOneAsync(RawLegacy(
            GuidFromOrdinal(60),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(-1)));
        await _raw.Indexes.DropOneAsync(ScalarClaimIndex);
        await _raw.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("Status")
                .Ascending("CreatedAtUtcTicksV1"),
            new CreateIndexOptions { Name = ScalarClaimIndex }));
        var before = await _raw.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync(Runner(stateRepository), activate: true));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_SELECTED_INDEX_INVALID", error.Message);
        Assert.Null(await stateRepository.GetAsync());
        Assert.Equal(before, await _raw.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync());
    }

    [Theory]
    [InlineData(AuditOutboxTemporalStorageMigrationRunner.BeforeFirstBatch)]
    [InlineData(AuditOutboxTemporalStorageMigrationRunner.AfterRowBeforeCheckpoint)]
    [InlineData(AuditOutboxTemporalStorageMigrationRunner.AfterCheckpoint)]
    [InlineData(AuditOutboxTemporalStorageMigrationRunner.AfterCompletedBeforeVerification)]
    public async Task MigrationRunner_CrashAtCheckpoint_ReplayCompletesWithoutLoss(string injectionPoint)
    {
        var rows = Enumerable.Range(0, 3)
            .Select(index => RawLegacy(
                GuidFromOrdinal(index + 20),
                DateTimeOffset.UtcNow.AddMinutes(-index - 1),
                DateTimeOffset.UtcNow.AddHours(-index - 1)))
            .ToArray();
        await _raw.InsertManyAsync(rows);
        var stateRepository = new AuditOutboxTemporalMigrationRepository(_database);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var crashing = new AuditOutboxTemporalStorageMigrationRunner(_database, stateRepository, clock);
        var injected = false;
        crashing.FailureInjector = (checkpoint, _) =>
        {
            if (!injected && checkpoint == injectionPoint)
            {
                injected = true;
                throw new InvalidOperationException("INJECTED_CRASH");
            }

            return Task.CompletedTask;
        };

        var crash = await Assert.ThrowsAsync<InvalidOperationException>(() => crashing.RunAsync(
            AuditOutboxTemporalMigrationState.ExactId,
            AuditOutboxTemporalMigrationState.ExactTargetVersion,
            batchSize: 2,
            leaseDuration: TimeSpan.FromMinutes(1),
            leaseOwner: "recovery-owner",
            activateScalarClaims: false,
            selectedIndexName: ScalarClaimIndex));
        Assert.Equal("INJECTED_CRASH", crash.Message);
        Assert.Equal(
            AuditOutboxTemporalMigrationState.Phases.RecoveryRequired,
            (await stateRepository.GetAsync())!.Phase);

        var replay = new AuditOutboxTemporalStorageMigrationRunner(_database, stateRepository, clock);
        var completed = await replay.RunAsync(
            AuditOutboxTemporalMigrationState.ExactId,
            AuditOutboxTemporalMigrationState.ExactTargetVersion,
            batchSize: 2,
            leaseDuration: TimeSpan.FromMinutes(1),
            leaseOwner: "recovery-owner",
            activateScalarClaims: false,
            selectedIndexName: ScalarClaimIndex);

        Assert.Equal(AuditOutboxTemporalMigrationState.Phases.CompletionVerified, completed.Phase);
        Assert.Equal(3, completed.ScannedCount);
        Assert.Equal(3, completed.MigratedCount + completed.AlreadyCurrentCount);
        Assert.Equal(3, await _raw.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.All(await _raw.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(), row => Assert.Equal(
            AuditOutboxTemporalStorageCompatibility.InspectionKind.Current,
            AuditOutboxTemporalStorageCompatibility.Inspect(row).Kind));
    }

    private async Task<BsonDocument> RawByIdAsync(Guid id) => await _raw
        .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
        .SingleAsync();

    private AuditOutboxTemporalStorageMigrationRunner Runner(
        AuditOutboxTemporalMigrationRepository stateRepository) => new(
        _database,
        stateRepository,
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)));

    private static Task<AuditOutboxTemporalMigrationState> RunAsync(
        AuditOutboxTemporalStorageMigrationRunner runner,
        bool activate) => runner.RunAsync(
        AuditOutboxTemporalMigrationState.ExactId,
        AuditOutboxTemporalMigrationState.ExactTargetVersion,
        batchSize: 2,
        leaseDuration: TimeSpan.FromMinutes(1),
        leaseOwner: "fu02-runner",
        activateScalarClaims: activate,
        selectedIndexName: ScalarClaimIndex);

    private async Task<AuditOutboxTemporalMigrationRepository> InsertActiveStateAsync(long scannedCount)
    {
        var collection = _database.GetCollection<AuditOutboxTemporalMigrationState>(
            AuditCollectionNames.AuditOutboxTemporalMigrations);
        await collection.InsertOneAsync(new AuditOutboxTemporalMigrationState
        {
            Phase = AuditOutboxTemporalMigrationState.Phases.CutoverActive,
            ScannedCount = scannedCount,
            AlreadyCurrentCount = scannedCount,
            SourceFingerprint = new string('d', 64),
            StartedAtUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            ActivationVersion = AuditOutboxTemporalMigrationState.ExactTargetVersion,
            SelectedIndexName = ScalarClaimIndex
        });
        return new AuditOutboxTemporalMigrationRepository(_database);
    }

    private AuditOutboxWriteRequest Request() => new()
    {
        TenantId = _tenantId,
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = $"fu02:{Guid.NewGuid():N}",
        RequestType = "FU02.Test",
        Operation = AuditOperation.Create,
        EntityType = "AuditOutboxTemporalTest",
        EntityId = Guid.NewGuid(),
        Payload = new Dictionary<string, object?> { ["safe"] = true }
    };

    private BsonDocument RawLegacy(
        Guid id,
        DateTimeOffset nextAttempt,
        DateTimeOffset createdAt) => new()
    {
        ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
        ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
        ["CorrelationId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
        ["IdempotencyKey"] = $"legacy:{Guid.NewGuid():N}",
        ["RequestType"] = "FU02.Legacy",
        ["Operation"] = (int)AuditOperation.Create,
        ["EntityType"] = "AuditOutboxTemporalTest",
        ["Payload"] = new BsonDocument(),
        ["Status"] = (int)AuditOutboxStatus.Pending,
        ["Attempts"] = 0,
        ["NextAttemptAtUtc"] = Legacy(nextAttempt),
        ["CreatedAtUtc"] = Legacy(createdAt),
        ["LastError"] = BsonNull.Value
    };

    private static BsonArray Legacy(DateTimeOffset value) => new()
    {
        value.Ticks,
        (int)value.Offset.TotalMinutes
    };

    private static TimeSpan OffsetFor(int index) => (index % 3) switch
    {
        0 => TimeSpan.FromHours(14),
        1 => TimeSpan.Zero,
        _ => TimeSpan.FromHours(-12)
    };

    private static Guid GuidFromOrdinal(int ordinal)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[15] = checked((byte)(ordinal % 256));
        bytes[14] = checked((byte)(ordinal / 256));
        return new Guid(bytes, bigEndian: true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

public sealed class AuditOutboxTemporalReplicaSetFixture : IAsyncLifetime
{
    private const string ConnectionVariable = "DITEN_FU02_REPLICA_MONGO";
    private const string MongodPathVariable = "DITEN_FU02_MONGOD_PATH";
    private OwnedMongoProcess? _replicaProcess;
    private OwnedMongoProcess? _standaloneProcess;

    public string ConnectionString { get; private set; } = string.Empty;
    public string StandaloneConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            var mongodPath = ResolveMongodPath();
            _standaloneProcess = await OwnedMongoProcess.StartAsync(mongodPath, replicaSetName: null);
            await AssertStandaloneAsync(_standaloneProcess);
            StandaloneConnectionString = _standaloneProcess.DirectConnectionString;

            var configuredConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
            if (!string.IsNullOrWhiteSpace(configuredConnection))
            {
                await AssertWritableReplicaSetAsync(configuredConnection, ownedProcess: null);
                ConnectionString = configuredConnection;
                return;
            }

            var replicaSetName = $"fu02rs{Guid.NewGuid():N}";
            _replicaProcess = await OwnedMongoProcess.StartAsync(mongodPath, replicaSetName);
            await InitiateReplicaSetAsync(_replicaProcess, replicaSetName);
            var replicaConnection = _replicaProcess.ReplicaConnectionString(replicaSetName);
            await AssertWritableReplicaSetAsync(replicaConnection, _replicaProcess);
            ConnectionString = replicaConnection;
        }
        catch (Exception original)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(original, cleanup);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var owned in new[] { _replicaProcess, _standaloneProcess })
        {
            if (owned is null)
            {
                continue;
            }

            try
            {
                await owned.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        _replicaProcess = null;
        _standaloneProcess = null;
        if (failures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static async Task AssertStandaloneAsync(OwnedMongoProcess process)
    {
        await WaitForHelloAsync(
            process.DirectConnectionString,
            process,
            hello => !hello.Contains("setName") &&
                     hello.TryGetValue("isWritablePrimary", out var primary) &&
                     primary.ToBoolean());
    }

    private static async Task InitiateReplicaSetAsync(OwnedMongoProcess process, string replicaSetName)
    {
        var settings = MongoClientSettings.FromConnectionString(process.DirectConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
        var admin = new MongoClient(settings).GetDatabase("admin");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            process.ThrowIfExited();
            try
            {
                var configuration = new BsonDocument
                {
                    { "_id", replicaSetName },
                    {
                        "members",
                        new BsonArray
                        {
                            new BsonDocument
                            {
                                { "_id", 0 },
                                { "host", $"127.0.0.1:{process.Port}" }
                            }
                        }
                    }
                };
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", configuration));
                return;
            }
            catch (MongoCommandException exception) when (exception.CodeName == "AlreadyInitialized")
            {
                return;
            }
            catch (MongoException)
            {
                await Task.Delay(200);
            }
            catch (TimeoutException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException(
            $"AUDIT_OUTBOX_TEMPORAL_TEST_REPLICA_INIT_TIMEOUT{Environment.NewLine}{process.RecentLog()}");
    }

    private static async Task AssertWritableReplicaSetAsync(
        string connectionString,
        OwnedMongoProcess? ownedProcess)
    {
        await WaitForHelloAsync(
            connectionString,
            ownedProcess,
            hello => hello.TryGetValue("setName", out var setName) && setName.IsString &&
                     hello.TryGetValue("isWritablePrimary", out var primary) &&
                     primary.ToBoolean());
    }

    private static async Task<BsonDocument> WaitForHelloAsync(
        string connectionString,
        OwnedMongoProcess? ownedProcess,
        Func<BsonDocument, bool> isExpectedTopology)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(750);
        var admin = new MongoClient(settings).GetDatabase("admin");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            ownedProcess?.ThrowIfExited();
            try
            {
                var hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (isExpectedTopology(hello))
                {
                    return hello;
                }
            }
            catch (MongoException)
            {
            }
            catch (TimeoutException)
            {
            }

            await Task.Delay(200);
        }

        throw new TimeoutException(
            $"AUDIT_OUTBOX_TEMPORAL_TEST_MONGO_READY_TIMEOUT{Environment.NewLine}{ownedProcess?.RecentLog()}");
    }

    private static string ResolveMongodPath()
    {
        var configured = Environment.GetEnvironmentVariable(MongodPathVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var exact = Path.GetFullPath(configured);
            if (!File.Exists(exact))
            {
                throw new FileNotFoundException($"{MongodPathVariable}_NOT_FOUND", exact);
            }

            return exact;
        }

        var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";
        var pathCandidates = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, executable));
        var pathMatch = pathCandidates.FirstOrDefault(File.Exists);
        if (pathMatch is not null)
        {
            return Path.GetFullPath(pathMatch);
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var serverRoot = Path.Combine(programFiles, "MongoDB", "Server");
            if (Directory.Exists(serverRoot))
            {
                var installed = Directory.EnumerateFiles(serverRoot, "mongod.exe", SearchOption.AllDirectories)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (installed is not null)
                {
                    return installed;
                }
            }
        }
        else
        {
            var installed = new[] { "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/usr/bin/mongod" }
                .FirstOrDefault(File.Exists);
            if (installed is not null)
            {
                return installed;
            }
        }

        throw new FileNotFoundException(
            $"AUDIT_OUTBOX_TEMPORAL_TEST_MONGOD_REQUIRED: install mongod or set {MongodPathVariable}; "
            + $"{ConnectionVariable} overrides only the positive replica-set URI");
    }

    private sealed class OwnedMongoProcess : IAsyncDisposable
    {
        private readonly ConcurrentQueue<string> _processLog = new();
        private Process? _process;
        private string? _directory;

        private OwnedMongoProcess(int port, string directory)
        {
            Port = port;
            _directory = directory;
        }

        public int Port { get; }
        public string DirectConnectionString => $"mongodb://127.0.0.1:{Port}/?directConnection=true";

        public string ReplicaConnectionString(string replicaSetName) =>
            $"mongodb://127.0.0.1:{Port}/?replicaSet={replicaSetName}&directConnection=true";

        public static async Task<OwnedMongoProcess> StartAsync(string mongodPath, string? replicaSetName)
        {
            var port = ReserveLoopbackPort();
            var directory = Path.Combine(Path.GetTempPath(), $"diten_fu02_mongo_{Guid.NewGuid():N}");
            var owned = new OwnedMongoProcess(port, directory);
            try
            {
                Directory.CreateDirectory(directory);
                var startInfo = new ProcessStartInfo
                {
                    FileName = mongodPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("--dbpath");
                startInfo.ArgumentList.Add(directory);
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("--bind_ip");
                startInfo.ArgumentList.Add(IPAddress.Loopback.ToString());
                if (replicaSetName is not null)
                {
                    startInfo.ArgumentList.Add("--replSet");
                    startInfo.ArgumentList.Add(replicaSetName);
                }

                startInfo.ArgumentList.Add("--quiet");
                owned._process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                owned._process.OutputDataReceived += (_, args) => owned.Capture(args.Data);
                owned._process.ErrorDataReceived += (_, args) => owned.Capture(args.Data);
                if (!owned._process.Start())
                {
                    throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_TEST_MONGO_START_FAILED");
                }

                owned._process.BeginOutputReadLine();
                owned._process.BeginErrorReadLine();
                return owned;
            }
            catch (Exception original)
            {
                try
                {
                    await owned.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(original, cleanup);
                }

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
                throw;
            }
        }

        public void ThrowIfExited()
        {
            if (_process is not null && _process.HasExited)
            {
                throw new InvalidOperationException(
                    $"AUDIT_OUTBOX_TEMPORAL_TEST_MONGO_EXITED:{_process.ExitCode}{Environment.NewLine}{RecentLog()}");
            }
        }

        public string RecentLog() => string.Join(Environment.NewLine, _processLog);

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                        await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
                finally
                {
                    try
                    {
                        _process.Dispose();
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }

                    _process = null;
                }
            }

            if (_directory is not null)
            {
                var owned = Path.GetFullPath(_directory);
                var tempRoot = Path.GetFullPath(Path.GetTempPath());
                var scopedTempRoot = tempRoot.EndsWith(Path.DirectorySeparatorChar)
                    ? tempRoot
                    : tempRoot + Path.DirectorySeparatorChar;
                if (!owned.StartsWith(scopedTempRoot, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(owned).StartsWith("diten_fu02_mongo_", StringComparison.Ordinal))
                {
                    failures.Add(new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_TEST_CLEANUP_SCOPE_INVALID"));
                }
                else
                {
                    for (var attempt = 0; attempt < 30 && Directory.Exists(owned); attempt++)
                    {
                        try
                        {
                            Directory.Delete(owned, recursive: true);
                        }
                        catch (IOException) when (attempt < 29)
                        {
                            await Task.Delay(100);
                        }
                        catch (UnauthorizedAccessException) when (attempt < 29)
                        {
                            await Task.Delay(100);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                            break;
                        }
                    }

                    if (Directory.Exists(owned))
                    {
                        failures.Add(new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_TEST_CLEANUP_FAILED"));
                    }
                }

                _directory = null;
            }

            if (failures.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            if (failures.Count > 1)
            {
                throw new AggregateException(failures);
            }
        }

        private void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            _processLog.Enqueue(line);
            while (_processLog.Count > 100)
            {
                _processLog.TryDequeue(out _);
            }
        }

        private static int ReserveLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
