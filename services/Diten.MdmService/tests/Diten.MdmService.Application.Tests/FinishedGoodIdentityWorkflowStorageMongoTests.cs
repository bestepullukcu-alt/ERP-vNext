using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FinishedGoodIdentityWorkflowStorageMongoTests
    : IAsyncLifetime, IClassFixture<AuditIntentTemporalMongoFixture>
{
    private readonly AuditIntentTemporalMongoFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ConcurrentQueue<string> _mongoCommandEvents = new();
    private readonly Stopwatch _diagnosticClock = Stopwatch.StartNew();
    private readonly List<Guid> _tenantIds = [];
    private IMongoDatabase _database = null!;
    private IMongoCollection<FinishedGoodIdentityWorkflowOperation> _collection = null!;
    private const int MaximumBsonDocumentBytes = 1024 * 1024;

    public FinishedGoodIdentityWorkflowStorageMongoTests(
        AuditIntentTemporalMongoFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(_fixture.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ClusterConfigurator = cluster =>
        {
            cluster.Subscribe<CommandStartedEvent>(started =>
                RecordMongoCommandEvent("started", started.CommandName, started.RequestId, started.OperationId));
            cluster.Subscribe<CommandSucceededEvent>(succeeded =>
                RecordMongoCommandEvent(
                    "succeeded",
                    succeeded.CommandName,
                    succeeded.RequestId,
                    succeeded.OperationId,
                    $"duration-ms={succeeded.Duration.TotalMilliseconds:F3}"));
            cluster.Subscribe<CommandFailedEvent>(failed =>
            {
                var code = failed.Failure is MongoCommandException commandException
                    ? commandException.Code.ToString(CultureInfo.InvariantCulture)
                    : "none";
                var labels = failed.Failure is MongoException mongoException
                    ? string.Join(',', mongoException.ErrorLabels.OrderBy(label => label, StringComparer.Ordinal))
                    : string.Empty;
                RecordMongoCommandEvent(
                    "failed",
                    failed.CommandName,
                    failed.RequestId,
                    failed.OperationId,
                    $"duration-ms={failed.Duration.TotalMilliseconds:F3}; exception={failed.Failure.GetType().Name}; " +
                    $"code={code}; labels={(labels.Length == 0 ? "none" : labels)}");
            });
        };
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var hello = await _database.Client.GetDatabase("admin")
            .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
        var endpoint = MongoUrl.Create(_fixture.ReplicaConnectionString).Server;
        _output.WriteLine(
            "P1A_TEST_MONGO_TOPOLOGY endpoint={0}:{1}; setName={2}; primary={3}; database={4}",
            endpoint.Host,
            endpoint.Port,
            hello.GetValue("setName", "missing").AsString,
            hello.GetValue("isWritablePrimary", false).ToBoolean(),
            ProductLegalEntityScopeMongoCollection.DatabaseName);
        _collection = _database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
            FinishedGoodIdentityWorkflowOperationRepository.CollectionName);
    }

    public async Task DisposeAsync()
    {
        if (_tenantIds.Count > 0)
        {
            await _collection.DeleteManyAsync(item => _tenantIds.Contains(item.TenantId));
        }

        while (_mongoCommandEvents.TryDequeue(out var commandEvent))
        {
            _output.WriteLine(commandEvent);
        }
    }

    [Fact]
    public async Task Same_key_race_is_one_durable_operation_and_exact_replay_is_tenant_scoped()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "same-key", "a");
        var repository = Repository(tenantId);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => repository.ReserveAsync(facts.Reservation)));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(1, results.Count(result => !result.IsReplay));
        Assert.Single(await _collection.Find(item => item.TenantId == tenantId).ToListAsync());
        Assert.NotNull(await repository.GetByOperationIdAsync(facts.OperationId));
        Assert.Null(await Repository(TenantId()).GetByOperationIdAsync(facts.OperationId));
    }

    [Fact]
    public async Task Immutable_identity_drift_conflicts_for_finished_good_gsku_maker_and_snapshot()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "immutable", "b");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);

        var drifts = new[]
        {
            facts.Reservation with { FinishedGoodId = Guid.NewGuid() },
            facts.Reservation with { GskuId = Guid.NewGuid() },
            facts.Reservation with { MakerSubjectId = Guid.NewGuid() },
            facts.Reservation with { AdmissionScopeSnapshot = Snapshot(tenantId, facts.FinishedGoodId, facts.GskuId,
                facts.RevisionId, facts.MakerId, integrityFill: 'c') }
        };

        foreach (var drift in drifts)
        {
            var result = await repository.ReserveAsync(drift);
            Assert.False(result.Succeeded);
            Assert.Contains(result.ErrorCode, new[]
            {
                "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_INVALID",
                "FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT"
            });
        }
    }

    [Fact]
    public async Task Tombstone_and_terminal_records_do_not_release_an_identity_for_reuse()
    {
        var tenantId = TenantId();
        var tombstone = Facts(tenantId, "tombstone", "c");
        var terminal = Facts(tenantId, "terminal", "d");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(tombstone.Reservation)).Succeeded);
        Assert.True((await repository.ReserveAsync(terminal.Reservation)).Succeeded);

        await _collection.UpdateOneAsync(item => item.TenantId == tenantId && item.OperationId == tombstone.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.IsDeleted, true));
        await _collection.UpdateOneAsync(item => item.TenantId == tenantId && item.OperationId == terminal.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(
                item => item.Checkpoint, FinishedGoodIdentityWorkflowCheckpoint.Completed));

        var tombstoneReuse = await repository.ReserveAsync(tombstone.Reservation with { OperationFingerprint = Fingerprint('e') });
        var terminalReuse = await repository.ReserveAsync(terminal.Reservation with { OperationFingerprint = Fingerprint('e') });

        Assert.False(tombstoneReuse.Succeeded);
        Assert.False(terminalReuse.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", tombstoneReuse.ErrorCode);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", terminalReuse.ErrorCode);
    }

    [Fact]
    public async Task Ambiguous_real_mongo_write_requires_exact_tenant_scoped_readback_before_replay_success()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "ambiguous-write", "e");
        var writeConcernDatabase = _database.WithWriteConcern(new WriteConcern(2, TimeSpan.FromMilliseconds(250)));
        var repository = new FinishedGoodIdentityWorkflowOperationRepository(
            writeConcernDatabase,
            new TenantContext(tenantId));

        var result = await repository.ReserveAsync(facts.Reservation);

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Equal(facts.OperationId, result.Operation!.OperationId);
        Assert.Equal(facts.Fingerprint, result.Operation.OperationFingerprint);
        Assert.Equal(tenantId, result.Operation.TenantId);
    }

    [Fact]
    public async Task Lease_claim_is_single_writer_and_stale_or_expired_claim_cannot_physically_advance()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "lease", "f");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);

        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => repository.TryClaimAsync(new(
            facts.OperationId, facts.Fingerprint, [FinishedGoodIdentityWorkflowCheckpoint.Prepared], $"owner-{index}", 0,
            TimeSpan.FromSeconds(2).Ticks))));
        if (claims.All(claim => claim is null))
        {
            await FailWithAllNullClaimDiagnosticsAsync(repository, tenantId, facts);
        }

        var winner = Assert.Single(claims, claim => claim is not null)!;

        await WaitUntilAfterAsync(winner.LeaseUntilUtcTicksV1);
        var expired = await repository.AdvanceAsync(winner, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        var takeover = await repository.TryClaimAsync(new(
            facts.OperationId, facts.Fingerprint, [FinishedGoodIdentityWorkflowCheckpoint.Prepared], "owner-takeover",
            winner.LeaseGeneration, TimeSpan.FromMinutes(1).Ticks));

        Assert.False(expired.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM", expired.ErrorCode);
        Assert.NotNull(takeover);
        Assert.Equal(winner.LeaseGeneration + 1, takeover!.LeaseGeneration);
    }

    [Fact]
    public async Task Concurrent_claims_never_create_two_active_owners_for_the_same_generation()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "claim-safety", "7");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);

        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            $"claim-safety-owner-{index}",
            0,
            TimeSpan.FromMinutes(1).Ticks))));
        var persisted = await Raw(facts.OperationId, tenantId);
        await WritePersistedClaimStateAsync("single-writer-long-lease", tenantId, facts.OperationId, persisted);

        var winner = Assert.Single(claims, claim => claim is not null)!;
        Assert.Equal(1, winner.LeaseGeneration);
        Assert.Equal(1, persisted[nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration)].ToInt64());
        Assert.Equal(1, persisted[nameof(FinishedGoodIdentityWorkflowOperation.Version)].ToInt64());
        Assert.Equal(winner.LeaseUntilUtcTicksV1,
            persisted[nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1)].AsInt64);
        Assert.True(winner.LeaseUntilUtcTicksV1 > DateTimeOffset.UtcNow.UtcTicks);
        Assert.Equal(
            HashDiagnosticValue(winner.LeaseOwner),
            HashDiagnosticValue(persisted[nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner)].AsString));
    }

    [Fact]
    public async Task Expired_blocked_advance_is_fail_closed_and_fresh_takeover_preserves_liveness()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "claim-expiry-liveness", "8");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);
        var staleClaim = Assert.IsType<FinishedGoodIdentityWorkflowClaim>(await repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            "claim-expiry-stale-owner",
            0,
            TimeSpan.FromSeconds(2).Ticks)));

        var commandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedRepository = Repository(tenantId, commandStarted);
        using var blocker = await _database.Client.StartSessionAsync();
        blocker.StartTransaction();
        const string synchronizationFailure = "p1a-claim-diag-synchronization";
        await RawCollection.UpdateOneAsync(
            blocker,
            RawFilter(facts.OperationId, tenantId),
            Builders<BsonDocument>.Update.Set("LastFailureCode", synchronizationFailure));
        var beforeAdvance = await Raw(facts.OperationId, tenantId);
        var expectedAfterSynchronization = beforeAdvance.DeepClone().AsBsonDocument;
        expectedAfterSynchronization["LastFailureCode"] = synchronizationFailure;

        var blockedAdvance = observedRepository.AdvanceAsync(staleClaim, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAfterAsync(staleClaim.LeaseUntilUtcTicksV1);
        await blocker.CommitTransactionAsync();
        var expiredResult = await blockedAdvance;
        var afterExpiredAdvance = await Raw(facts.OperationId, tenantId);
        await WritePersistedClaimStateAsync(
            "expired-blocked-advance",
            tenantId,
            facts.OperationId,
            afterExpiredAdvance);

        Assert.False(expiredResult.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM", expiredResult.ErrorCode);
        Assert.Equal(expectedAfterSynchronization, afterExpiredAdvance);

        var takeover = Assert.IsType<FinishedGoodIdentityWorkflowClaim>(await repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            "claim-expiry-fresh-owner",
            staleClaim.LeaseGeneration,
            TimeSpan.FromMinutes(1).Ticks)));
        var staleRetry = await repository.AdvanceAsync(staleClaim, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        var freshAdvance = await repository.AdvanceAsync(takeover, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        var finalState = await Raw(facts.OperationId, tenantId);
        await WritePersistedClaimStateAsync("fresh-takeover-advanced", tenantId, facts.OperationId, finalState);

        Assert.Equal(staleClaim.LeaseGeneration + 1, takeover.LeaseGeneration);
        Assert.False(staleRetry.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM", staleRetry.ErrorCode);
        Assert.True(freshAdvance.Succeeded);
        Assert.Equal(
            (int)FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            finalState[nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint)].AsInt32);
    }

    [Fact]
    public async Task Recovery_and_partition_discovery_share_terminal_exclusions_and_scalar_cursor_ordering()
    {
        var tenantA = TenantId();
        var tenantB = TenantId();
        var repository = Repository(tenantA);
        var activeFirst = Facts(tenantA, "due-a", "1", Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var activeSecond = Facts(tenantA, "due-b", "2", Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var excluded = Enum.GetValues<FinishedGoodIdentityWorkflowCheckpoint>()
            .Where(value => value is FinishedGoodIdentityWorkflowCheckpoint.Completed
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired
                or FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or FinishedGoodIdentityWorkflowCheckpoint.Superseded
                or FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay)
            .ToArray();
        var records = new[] { activeFirst, activeSecond }
            .Concat(excluded.Select((checkpoint, index) => Facts(tenantA, $"excluded-{index}", ((char)('3' + index)).ToString())))
            .Append(Facts(tenantB, "other-tenant", "9"))
            .ToArray();
        foreach (var record in records)
            Assert.True((await Repository(record.Reservation.AdmissionScopeSnapshot.TenantId).ReserveAsync(record.Reservation)).Succeeded);

        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(item => item.OperationId == activeFirst.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.NextAttemptAtUtcTicksV1, now - 2));
        await _collection.UpdateOneAsync(item => item.OperationId == activeSecond.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.NextAttemptAtUtcTicksV1, now - 1));
        foreach (var pair in excluded.Select((checkpoint, index) => (checkpoint, records[index + 2])))
            await _collection.UpdateOneAsync(item => item.OperationId == pair.Item2.OperationId,
                Builders<FinishedGoodIdentityWorkflowOperation>.Update
                    .Set(item => item.Checkpoint, pair.checkpoint)
                    .Set(item => item.NextAttemptAtUtcTicksV1, now - 1));

        var pageOne = await repository.DiscoverRecoverableAsync(1);
        var pageTwo = await repository.DiscoverRecoverableAsync(1, pageOne.NextCursor);
        var discovery = new FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository(_database);
        var partitions = await discovery.DiscoverAsync(null, 10);

        Assert.Equal(activeFirst.OperationId, Assert.Single(pageOne.Operations).OperationId);
        Assert.Equal(activeSecond.OperationId, Assert.Single(pageTwo.Operations).OperationId);
        Assert.Contains(tenantA, partitions.TenantIds);
        Assert.Contains(tenantB, partitions.TenantIds);
        Assert.DoesNotContain(pageOne.Operations.Concat(pageTwo.Operations), operation => excluded.Contains(operation.Checkpoint));
    }

    [Fact]
    public async Task Full_persisted_bson_is_bounded_on_reserve_and_claim_and_contains_no_credentials()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "bson", "a");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);
        var before = await Raw(facts.OperationId, tenantId);
        var claim = await repository.TryClaimAsync(new(
            facts.OperationId, facts.Fingerprint, [FinishedGoodIdentityWorkflowCheckpoint.Prepared], "bson-owner", 0,
            TimeSpan.FromMinutes(1).Ticks));
        var claimed = await Raw(facts.OperationId, tenantId);
        var advanced = await repository.AdvanceAsync(claim!, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        var afterAdvance = await Raw(facts.OperationId, tenantId);

        Assert.NotNull(claim);
        Assert.True(advanced.Succeeded);
        Assert.True(before.ToBson().Length <= 1024 * 1024);
        Assert.True(claimed.ToBson().Length <= 1024 * 1024);
        Assert.True(afterAdvance.ToBson().Length <= 1024 * 1024);
        Assert.DoesNotContain(afterAdvance.Names, name => name.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(afterAdvance.Names, name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(afterAdvance.Names, name => name.Contains("credential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Advance_started_with_valid_lease_cannot_mutate_after_mongo_evaluates_expired_lease()
    {
        var tenantId = TenantId();
        var facts = Facts(tenantId, "physical-expiry", "b");
        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);
        var claim = await repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            "physical-expiry-owner",
            0,
            TimeSpan.FromSeconds(2).Ticks));
        Assert.NotNull(claim);

        var commandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedRepository = Repository(tenantId, commandStarted);
        using var blocker = await _database.Client.StartSessionAsync();
        blocker.StartTransaction();
        const string synchronizationFailure = "p1a-test-synchronization";
        await RawCollection.UpdateOneAsync(
            blocker,
            RawFilter(facts.OperationId, tenantId),
            Builders<BsonDocument>.Update.Set("LastFailureCode", synchronizationFailure));

        var before = await Raw(facts.OperationId, tenantId);
        var expectedAfterSynchronization = before.DeepClone().AsBsonDocument;
        expectedAfterSynchronization["LastFailureCode"] = synchronizationFailure;
        var advanceTask = observedRepository.AdvanceAsync(claim!, new(
            FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None));
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAfterAsync(claim!.LeaseUntilUtcTicksV1);
        await blocker.CommitTransactionAsync();
        var result = await advanceTask;
        var after = await Raw(facts.OperationId, tenantId);

        var rejected = !result.Succeeded
            && result.ErrorCode == "FINISHED_GOOD_IDENTITY_WORKFLOW_STALE_CLAIM";
        var allFactsUnchanged = expectedAfterSynchronization.Equals(after);
        Assert.True(
            rejected && allFactsUnchanged,
            $"expired-write-rejected={rejected}; all-facts-unchanged={allFactsUnchanged}; " +
            $"checkpoint-before={expectedAfterSynchronization["Checkpoint"]}; checkpoint-after={after["Checkpoint"]}; " +
            $"version-before={expectedAfterSynchronization["Version"]}; version-after={after["Version"]}");
    }

    [Fact]
    public async Task Retry_eligibility_null_due_and_future_is_identical_across_discovery_and_claim()
    {
        var nullTenant = TenantId();
        var dueTenant = TenantId();
        var futureTenant = TenantId();
        var nullFacts = Facts(nullTenant, "retry-null", "c");
        var dueFacts = Facts(dueTenant, "retry-due", "d");
        var futureFacts = Facts(futureTenant, "retry-future", "e");
        Assert.True((await Repository(nullTenant).ReserveAsync(nullFacts.Reservation)).Succeeded);
        Assert.True((await Repository(dueTenant).ReserveAsync(dueFacts.Reservation)).Succeeded);
        Assert.True((await Repository(futureTenant).ReserveAsync(futureFacts.Reservation)).Succeeded);

        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(item => item.TenantId == dueTenant && item.OperationId == dueFacts.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.NextAttemptAtUtcTicksV1, now - TimeSpan.TicksPerSecond));
        await _collection.UpdateOneAsync(item => item.TenantId == futureTenant && item.OperationId == futureFacts.OperationId,
            Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.NextAttemptAtUtcTicksV1, now + TimeSpan.TicksPerMinute));

        var nullOperations = await Repository(nullTenant).DiscoverRecoverableAsync(10);
        var dueOperations = await Repository(dueTenant).DiscoverRecoverableAsync(10);
        var futureOperations = await Repository(futureTenant).DiscoverRecoverableAsync(10);
        var partitions = await new FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository(_database)
            .DiscoverAsync(null, 100);
        var futureBefore = await Raw(futureFacts.OperationId, futureTenant);
        var nullClaim = await ClaimAsync(Repository(nullTenant), nullFacts, "retry-null-owner");
        var dueClaim = await ClaimAsync(Repository(dueTenant), dueFacts, "retry-due-owner");
        var futureClaim = await ClaimAsync(Repository(futureTenant), futureFacts, "retry-future-owner");
        var futureAfter = await Raw(futureFacts.OperationId, futureTenant);

        Assert.Contains(nullOperations.Operations, item => item.OperationId == nullFacts.OperationId);
        Assert.Contains(dueOperations.Operations, item => item.OperationId == dueFacts.OperationId);
        Assert.DoesNotContain(futureOperations.Operations, item => item.OperationId == futureFacts.OperationId);
        Assert.Contains(nullTenant, partitions.TenantIds);
        Assert.Contains(dueTenant, partitions.TenantIds);
        Assert.DoesNotContain(futureTenant, partitions.TenantIds);
        Assert.NotNull(nullClaim);
        Assert.NotNull(dueClaim);
        Assert.True(
            futureClaim is null && futureBefore.Equals(futureAfter),
            $"future-claim-null={futureClaim is null}; future-row-unchanged={futureBefore.Equals(futureAfter)}");
    }

    [Fact]
    public async Task Full_bson_ceiling_is_atomic_for_claim_and_advance_and_preserves_unknown_fields()
    {
        var claimTenant = TenantId();
        var claimFacts = Facts(claimTenant, "bson-claim-boundary", "f");
        var claimRepository = Repository(claimTenant);
        Assert.True((await claimRepository.ReserveAsync(claimFacts.Reservation)).Succeeded);
        var claimBefore = await Raw(claimFacts.OperationId, claimTenant);
        var claimPadded = PaddedClone(claimBefore, MaximumBsonDocumentBytes - 32);
        var claimExpected = claimPadded.DeepClone().AsBsonDocument;
        var claimCommandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedClaimRepository = Repository(claimTenant, claimCommandStarted);
        using var claimBlocker = await _database.Client.StartSessionAsync();
        claimBlocker.StartTransaction();
        await RawCollection.UpdateOneAsync(
            claimBlocker,
            RawFilter(claimFacts.OperationId, claimTenant),
            Builders<BsonDocument>.Update.Set("P1aUnknownField", claimPadded["P1aUnknownField"]));
        FinishedGoodIdentityWorkflowClaim? boundaryClaim = null;
        var claimTask = Task.Run(async () =>
        {
            boundaryClaim = await observedClaimRepository.TryClaimAsync(new(
                claimFacts.OperationId,
                claimFacts.Fingerprint,
                [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
                new string('o', 128),
                0,
                TimeSpan.FromMinutes(1).Ticks));
        });
        await claimCommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await claimBlocker.CommitTransactionAsync();
        var claimException = await Record.ExceptionAsync(() => claimTask);
        var claimAfter = await Raw(claimFacts.OperationId, claimTenant);

        var advanceTenant = TenantId();
        var advanceFacts = Facts(advanceTenant, "bson-advance-race", "1");
        var advanceRepository = Repository(advanceTenant);
        Assert.True((await advanceRepository.ReserveAsync(advanceFacts.Reservation)).Succeeded);
        var advanceClaim = await ClaimAsync(advanceRepository, advanceFacts, "a");
        Assert.NotNull(advanceClaim);
        var advanceBefore = await Raw(advanceFacts.OperationId, advanceTenant);
        var padded = PaddedClone(advanceBefore, MaximumBsonDocumentBytes - 32);
        var advanceExpected = padded.DeepClone().AsBsonDocument;
        var commandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedRepository = Repository(advanceTenant, commandStarted);
        using var blocker = await _database.Client.StartSessionAsync();
        blocker.StartTransaction();
        await RawCollection.UpdateOneAsync(
            blocker,
            RawFilter(advanceFacts.OperationId, advanceTenant),
            Builders<BsonDocument>.Update.Set("P1aUnknownField", padded["P1aUnknownField"]));
        var advanceTask = observedRepository.AdvanceAsync(advanceClaim!, new(
            FinishedGoodIdentityWorkflowCheckpoint.Prepared,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks,
            new string('r', 128),
            true));
        await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await blocker.CommitTransactionAsync();
        FinishedGoodIdentityWorkflowAdvanceResult? advanceResult = null;
        var advanceException = await Record.ExceptionAsync(async () => advanceResult = await advanceTask);
        var advanceAfter = await Raw(advanceFacts.OperationId, advanceTenant);

        var preserveTenant = TenantId();
        var preserveFacts = Facts(preserveTenant, "bson-unknown-preserve", "2");
        var preserveRepository = Repository(preserveTenant);
        Assert.True((await preserveRepository.ReserveAsync(preserveFacts.Reservation)).Succeeded);
        await RawCollection.UpdateOneAsync(
            RawFilter(preserveFacts.OperationId, preserveTenant),
            Builders<BsonDocument>.Update.Set("P1aUnknownField", "preserve-me"));
        FinishedGoodIdentityWorkflowClaim? preserveClaim = null;
        var preserveException = await Record.ExceptionAsync(async () =>
        {
            preserveClaim = await ClaimAsync(preserveRepository, preserveFacts, "preserve-owner");
            Assert.NotNull(preserveClaim);
            Assert.True((await preserveRepository.AdvanceAsync(preserveClaim!, new(
                FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.None))).Succeeded);
        });
        var preserved = await Raw(preserveFacts.OperationId, preserveTenant);

        var maximumTenant = TenantId();
        var maximumFacts = MaximumValidFacts(maximumTenant);
        var maximumReserve = await Repository(maximumTenant).ReserveAsync(maximumFacts.Reservation);
        var maximumRaw = await Raw(maximumFacts.OperationId, maximumTenant);
        var maximumValidBytes = maximumRaw.ToBson().Length;

        var claimRejectedWithoutException = claimException is null && boundaryClaim is null;
        var claimUnchanged = claimExpected.Equals(claimAfter);
        var advanceRejectedWithoutException = advanceException is null && advanceResult is { Succeeded: false };
        var advanceUnchanged = advanceExpected.Equals(advanceAfter);
        var unknownPreserved = preserveException is null
            && preserved.TryGetValue("P1aUnknownField", out var unknown)
            && unknown.IsString
            && unknown.AsString == "preserve-me";
        Assert.True(
            claimRejectedWithoutException
            && claimUnchanged
            && claimAfter.ToBson().Length <= MaximumBsonDocumentBytes
            && advanceRejectedWithoutException
            && advanceUnchanged
            && advanceAfter.ToBson().Length <= MaximumBsonDocumentBytes
            && unknownPreserved
            && maximumReserve.Succeeded
            && maximumValidBytes <= MaximumBsonDocumentBytes,
            $"claim-rejected={claimRejectedWithoutException}; claim-unchanged={claimUnchanged}; " +
            $"claim-bytes={claimAfter.ToBson().Length}; advance-rejected={advanceRejectedWithoutException}; " +
            $"advance-unchanged={advanceUnchanged}; advance-bytes={advanceAfter.ToBson().Length}; " +
            $"unknown-preserved={unknownPreserved}; maximum-valid-bytes={maximumValidBytes}");
    }

    [Fact]
    public async Task Persisted_stale_malformed_and_legacy_admission_snapshots_are_rejected_without_mutation()
    {
        var cases = new[]
        {
            (Name: "stale", Mutate: (Action<BsonDocument>)(snapshot => snapshot["ScopePolicyVersion"] = snapshot["ScopePolicyVersion"].AsInt32 + 1)),
            (Name: "malformed", Mutate: (Action<BsonDocument>)(snapshot => snapshot["IntegrityFingerprint"] = "not-a-fingerprint")),
            (Name: "legacy", Mutate: (Action<BsonDocument>)(snapshot => snapshot["SnapshotVersion"] = 0))
        };
        var outcomes = new List<string>();

        foreach (var testCase in cases)
        {
            var tenantId = TenantId();
            var facts = Facts(tenantId, $"persisted-{testCase.Name}", "4");
            var repository = Repository(tenantId);
            Assert.True((await repository.ReserveAsync(facts.Reservation)).Succeeded);
            var raw = await Raw(facts.OperationId, tenantId);
            var snapshot = raw["AdmissionScopeSnapshot"].AsBsonDocument;
            testCase.Mutate(snapshot);
            await RawCollection.ReplaceOneAsync(RawFilter(facts.OperationId, tenantId), raw);
            var before = await Raw(facts.OperationId, tenantId);

            FinishedGoodIdentityWorkflowOperation? read = null;
            var readException = await Record.ExceptionAsync(async () => read = await repository.GetByOperationIdAsync(facts.OperationId));
            var replay = await repository.ReserveAsync(facts.Reservation);
            FinishedGoodIdentityWorkflowClaim? claim = null;
            var claimException = await Record.ExceptionAsync(async () => claim = await ClaimAsync(repository, facts, $"persisted-{testCase.Name}-owner"));
            var after = await Raw(facts.OperationId, tenantId);
            var readRejected = read is null && (readException is null or InvalidOperationException);
            var replayRejected = !replay.Succeeded;
            var claimRejected = claim is null && (claimException is null or InvalidOperationException);
            var unchanged = before.Equals(after);
            outcomes.Add($"{testCase.Name}:read={readRejected},replay={replayRejected},claim={claimRejected},unchanged={unchanged}");
        }

        Assert.All(outcomes, outcome => Assert.DoesNotContain("False", outcome, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<BsonDocument> Raw(Guid operationId, Guid tenantId) => await _database
        .GetCollection<BsonDocument>(FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
        .Find(new BsonDocument
        {
            ["TenantId"] = new BsonBinaryData(tenantId, GuidRepresentation.Standard),
            ["OperationId"] = new BsonBinaryData(operationId, GuidRepresentation.Standard)
        })
        .SingleAsync();

    private async Task FailWithAllNullClaimDiagnosticsAsync(
        FinishedGoodIdentityWorkflowOperationRepository repository,
        Guid tenantId,
        (Guid OperationId, Guid FinishedGoodId, Guid GskuId, Guid RevisionId, Guid MakerId,
            string Fingerprint, FinishedGoodIdentityWorkflowReservation Reservation) facts)
    {
        var persisted = await Raw(facts.OperationId, tenantId);
        await WritePersistedClaimStateAsync("all-null-observed", tenantId, facts.OperationId, persisted);
        var persistedGeneration = persisted[nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration)].ToInt64();
        var persistedVersion = persisted[nameof(FinishedGoodIdentityWorkflowOperation.Version)].ToInt64();
        var owner = persisted.GetValue(nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), BsonNull.Value);
        var leaseUntil = persisted.GetValue(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1),
            BsonNull.Value);
        if (!leaseUntil.IsBsonNull)
        {
            await WaitUntilAfterAsync(leaseUntil.AsInt64);
        }

        var recovery = await repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            "all-null-recovery-owner",
            persistedGeneration,
            TimeSpan.FromMinutes(1).Ticks));
        var afterRecovery = await Raw(facts.OperationId, tenantId);
        await WritePersistedClaimStateAsync("all-null-recovery", tenantId, facts.OperationId, afterRecovery);

        var unexpectedMutation = persistedGeneration != 0 || persistedVersion != 0 || !owner.IsBsonNull;
        if (unexpectedMutation)
        {
            Assert.Fail(
                "P1A_ALL_NULL_PERSISTED_MUTATION: all contenders returned null but persisted claim facts changed; " +
                $"recovery-succeeded={recovery is not null}");
        }

        Assert.True(recovery is not null,
            "P1A_ALL_NULL_LIVENESS_FAILURE: all contenders returned null and a fresh claim could not progress after contention cleared.");
        Assert.Fail(
            "P1A_ALL_NULL_REPRODUCED_LIVENESS_OK: all contenders returned null; persisted state remained clean and a fresh claim progressed.");
    }

    private Task WritePersistedClaimStateAsync(
        string label,
        Guid tenantId,
        Guid operationId,
        BsonDocument persisted)
    {
        var owner = persisted.GetValue(nameof(FinishedGoodIdentityWorkflowOperation.LeaseOwner), BsonNull.Value);
        var leaseUntil = persisted.GetValue(
            nameof(FinishedGoodIdentityWorkflowOperation.LeaseUntilUtcTicksV1),
            BsonNull.Value);
        _output.WriteLine(
            "P1A_CLAIM_STATE label={0}; tenant={1:D}; operation={2:D}; elapsed-ms={3:F3}; " +
            "checkpoint={4}; version={5}; lease-generation={6}; owner={7}; lease-until={8}; now-utc-ticks={9}",
            label,
            tenantId,
            operationId,
            _diagnosticClock.Elapsed.TotalMilliseconds,
            persisted[nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint)].AsInt32,
            persisted[nameof(FinishedGoodIdentityWorkflowOperation.Version)].ToInt64(),
            persisted[nameof(FinishedGoodIdentityWorkflowOperation.LeaseGeneration)].ToInt64(),
            owner.IsBsonNull ? "none" : HashDiagnosticValue(owner.AsString),
            leaseUntil.IsBsonNull ? "none" : leaseUntil.AsInt64.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow.UtcTicks);
        return Task.CompletedTask;
    }

    private void RecordMongoCommandEvent(
        string phase,
        string commandName,
        int requestId,
        long? operationId,
        string outcome = "none") => _mongoCommandEvents.Enqueue(
            $"P1A_MONGO_COMMAND phase={phase}; elapsed-ms={_diagnosticClock.Elapsed.TotalMilliseconds:F3}; " +
            $"command={commandName}; request-id={requestId}; operation-id={operationId?.ToString(CultureInfo.InvariantCulture) ?? "none"}; {outcome}");

    private static string HashDiagnosticValue(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant()}";
    }

    private IMongoCollection<BsonDocument> RawCollection => _database
        .GetCollection<BsonDocument>(FinishedGoodIdentityWorkflowOperationRepository.CollectionName);

    private static FilterDefinition<BsonDocument> RawFilter(Guid operationId, Guid tenantId) =>
        Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard))
        & Builders<BsonDocument>.Filter.Eq("OperationId", new BsonBinaryData(operationId, GuidRepresentation.Standard));

    private Guid TenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenantIds.Add(tenantId);
        return tenantId;
    }

    private FinishedGoodIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new TenantContext(tenantId));

    private FinishedGoodIdentityWorkflowOperationRepository Repository(
        Guid tenantId,
        TaskCompletionSource commandStarted)
    {
        var settings = MongoClientSettings.FromConnectionString(_fixture.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ClusterConfigurator = cluster => cluster.Subscribe<CommandStartedEvent>(started =>
        {
            if (string.Equals(started.CommandName, "findAndModify", StringComparison.Ordinal))
            {
                commandStarted.TrySetResult();
            }
        });
        var database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        return new(database, new TenantContext(tenantId));
    }

    private static async Task WaitUntilAfterAsync(long utcTicks)
    {
        var remaining = utcTicks - DateTimeOffset.UtcNow.UtcTicks + TimeSpan.FromMilliseconds(200).Ticks;
        if (remaining > 0)
        {
            await Task.Delay(TimeSpan.FromTicks(remaining));
        }
    }

    private static Task<FinishedGoodIdentityWorkflowClaim?> ClaimAsync(
        FinishedGoodIdentityWorkflowOperationRepository repository,
        (Guid OperationId, Guid FinishedGoodId, Guid GskuId, Guid RevisionId, Guid MakerId,
            string Fingerprint, FinishedGoodIdentityWorkflowReservation Reservation) facts,
        string owner) => repository.TryClaimAsync(new(
            facts.OperationId,
            facts.Fingerprint,
            [FinishedGoodIdentityWorkflowCheckpoint.Prepared],
            owner,
            0,
            TimeSpan.FromMinutes(1).Ticks));

    private static BsonDocument PaddedClone(BsonDocument source, int targetBytes)
    {
        var low = 0;
        var high = targetBytes;
        BsonDocument best = source.DeepClone().AsBsonDocument;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = source.DeepClone().AsBsonDocument;
            candidate["P1aUnknownField"] = new string('x', middle);
            if (candidate.ToBson().Length <= targetBytes)
            {
                best = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
    }

    private static (Guid OperationId, Guid FinishedGoodId, Guid GskuId, Guid RevisionId, Guid MakerId,
        string Fingerprint, FinishedGoodIdentityWorkflowReservation Reservation) Facts(
        Guid tenantId, string key, string fingerprintFill, Guid? operationId = null)
    {
        var finishedGoodId = Guid.NewGuid();
        var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var resolvedOperationId = operationId ?? Guid.NewGuid();
        var fingerprint = Fingerprint(fingerprintFill[0]);
        return (resolvedOperationId, finishedGoodId, gskuId, revisionId, makerId, fingerprint, new(
            resolvedOperationId,
            finishedGoodId,
            gskuId,
            revisionId,
            makerId,
            0,
            key,
            fingerprint,
            Snapshot(tenantId, finishedGoodId, gskuId, revisionId, makerId),
            DateTimeOffset.UtcNow.UtcTicks));
    }

    private static FinishedGoodLifecycleAdmissionScopeSnapshot Snapshot(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid revisionId,
        Guid makerId,
        char integrityFill = 'a')
    {
        var commandId = Guid.NewGuid();
        var observedAt = DateTimeOffset.UtcNow.UtcTicks;
        var policyId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var legalEntityIds = new[] { Guid.NewGuid() };
        return FinishedGoodLifecycleAdmissionScopeSnapshot.Create(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            makerId,
            observedAt,
            policyId,
            1,
            ProductLegalEntityScopeMode.Scoped,
            rolloutId,
            1,
            ProductLegalEntityScopeRolloutMode.Enforced,
            legalEntityIds,
            SnapshotFingerprint(
                tenantId,
                finishedGoodId,
                gskuId,
                revisionId,
                commandId,
                makerId,
                observedAt,
                policyId,
                1,
                ProductLegalEntityScopeMode.Scoped,
                rolloutId,
                1,
                ProductLegalEntityScopeRolloutMode.Enforced,
                legalEntityIds));
    }

    private static (Guid OperationId, Guid FinishedGoodId, Guid GskuId, Guid RevisionId, Guid MakerId,
        string Fingerprint, FinishedGoodIdentityWorkflowReservation Reservation) MaximumValidFacts(Guid tenantId)
    {
        var operationId = Guid.NewGuid();
        var finishedGoodId = Guid.NewGuid();
        var gskuId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var observedAt = DateTimeOffset.UtcNow.UtcTicks;
        var policyId = Guid.NewGuid();
        var rolloutId = Guid.NewGuid();
        var legalEntityIds = Enumerable.Range(1, FinishedGoodLifecycleAdmissionScopeSnapshot.MaximumLegalEntityIds)
            .Select(value => Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))
            .ToArray();
        var snapshot = FinishedGoodLifecycleAdmissionScopeSnapshot.Create(
            tenantId,
            finishedGoodId,
            gskuId,
            revisionId,
            commandId,
            makerId,
            observedAt,
            policyId,
            int.MaxValue,
            ProductLegalEntityScopeMode.Scoped,
            rolloutId,
            int.MaxValue,
            ProductLegalEntityScopeRolloutMode.Enforced,
            legalEntityIds,
            SnapshotFingerprint(
                tenantId,
                finishedGoodId,
                gskuId,
                revisionId,
                commandId,
                makerId,
                observedAt,
                policyId,
                int.MaxValue,
                ProductLegalEntityScopeMode.Scoped,
                rolloutId,
                int.MaxValue,
                ProductLegalEntityScopeRolloutMode.Enforced,
                legalEntityIds));
        var fingerprint = Fingerprint('3');
        return (operationId, finishedGoodId, gskuId, revisionId, makerId, fingerprint, new(
            operationId,
            finishedGoodId,
            gskuId,
            revisionId,
            makerId,
            int.MaxValue,
            new string('k', 256),
            fingerprint,
            snapshot,
            long.MaxValue));
    }

    private static string SnapshotFingerprint(
        Guid tenantId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid revisionId,
        Guid commandId,
        Guid actorId,
        long observedAt,
        Guid policyId,
        int policyVersion,
        ProductLegalEntityScopeMode scopeMode,
        Guid rolloutId,
        int rolloutVersion,
        ProductLegalEntityScopeRolloutMode rolloutMode,
        IReadOnlyList<Guid> legalEntityIds)
    {
        var canonicalIds = legalEntityIds.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "contract", "finished-good-lifecycle-admission-snapshot");
        Append(hash, "snapshot-version", FinishedGoodLifecycleAdmissionScopeSnapshot.CurrentSnapshotVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "tenant-id", tenantId.ToString("D"));
        Append(hash, "finished-good-id", finishedGoodId.ToString("D"));
        Append(hash, "gsku-id", gskuId.ToString("D"));
        Append(hash, "product-definition-revision-id", revisionId.ToString("D"));
        Append(hash, "admission-command-id", commandId.ToString("D"));
        Append(hash, "admission-actor-subject-id", actorId.ToString("D"));
        Append(hash, "admission-observed-at-utc-ticks-v1", observedAt.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-policy-id", policyId.ToString("D"));
        Append(hash, "scope-policy-version", policyVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "scope-mode", ((int)scopeMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-state-id", rolloutId.ToString("D"));
        Append(hash, "rollout-version", rolloutVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, "rollout-mode", ((int)rolloutMode).ToString(CultureInfo.InvariantCulture));
        Append(hash, "legal-entity-count", canonicalIds.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < canonicalIds.Length; index++)
        {
            Append(hash, $"legal-entity-id-{index}", canonicalIds[index].ToString("D"));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string name, string value)
    {
        var encoded = Encoding.UTF8.GetBytes($"{name.Length}:{name}{value.Length}:{value}");
        hash.AppendData(encoded);
    }

    private static string Fingerprint(char fill) => new(fill, 64);

    private sealed class TenantContext(Guid tenantId) : ITenantContext
    {
        private Guid _tenantId = tenantId;

        public Guid TenantId => _tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => _tenantId = tenantId;
    }
}
