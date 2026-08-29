using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private const string DatabaseName = ProductLegalEntityScopeMongoCollection.DatabaseName;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProductIdentityWorkflowOperation> _collection = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_and_conflicts_on_any_identity_reuse()
    {
        var repository = Repository(_tenantId);
        var operation = Operation(_tenantId, "op-1", "fingerprint-A");

        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Operation(
            _tenantId, "op-1", "fingerprint-A", operation.OperationId,
            operation.GlobalProductId, operation.ExpectedProductVersion));
        var drift = await repository.ReserveAsync(Operation(
            _tenantId, "op-1", "fingerprint-B", operation.OperationId,
            operation.GlobalProductId, operation.ExpectedProductVersion));
        var crossTenant = await Repository(_tenantBId).GetByOperationIdAsync(operation.OperationId);

        Assert.True(first.Succeeded);
        Assert.False(first.IsReplay);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Null(crossTenant);
        Assert.Single(await _collection.Find(item => item.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Parallel_claim_has_one_winner_and_expiry_increments_generation()
    {
        var repository = Repository(_tenantId);
        var operation = Operation(_tenantId, "claim", "claim-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        var requests = Enumerable.Range(0, 8).Select(index => repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.Prepared],
            $"worker-{index}",
            now,
            now + TimeSpan.FromMinutes(1).Ticks)));

        var claims = await Task.WhenAll(requests);

        var winner = Assert.Single(claims, item => item is not null)!;
        Assert.Equal(1, winner.LeaseGeneration);
        var early = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.Prepared], "early", now + 1,
            now + TimeSpan.FromMinutes(2).Ticks));
        Assert.Null(early);
        var recovered = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.Prepared], "recovery",
            now + TimeSpan.FromMinutes(1).Ticks + 1,
            now + TimeSpan.FromMinutes(3).Ticks));
        Assert.NotNull(recovered);
        Assert.Equal(2, recovered!.LeaseGeneration);
    }

    [Fact]
    public async Task Checkpoint_CAS_persists_sanitized_proof_and_recovers_after_restart()
    {
        var repository = Repository(_tenantId);
        var operation = Operation(_tenantId, "crash", "crash-fingerprint");
        operation.WorkflowTemplateId = null;
        operation.WorkflowTemplateCode = "GLOBAL-PRODUCT-IDENTITY";
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = new DateTimeOffset(2026, 8, 29, 13, 0, 0, TimeSpan.Zero).UtcTicks;
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.Prepared], "worker-a", now,
            now + TimeSpan.FromMinutes(2).Ticks));
        Assert.NotNull(claim);
        var templateVersionId = Guid.NewGuid();
        var resolvedTemplateId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        Assert.True(await repository.AdvanceAsync(claim!, new GlobalProductIdentityWorkflowCheckpointMutation(
            GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now + 1,
            NextAttemptAtUtcTicksV1: now + 2,
            WorkflowInstanceId: workflowInstanceId,
            WorkflowTemplateId: resolvedTemplateId,
            WorkflowTemplateVersionId: templateVersionId,
            ReleaseLease: true)));
        Assert.False(await repository.AdvanceAsync(claim, new GlobalProductIdentityWorkflowCheckpointMutation(
            GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 2)));

        var restartedRepository = Repository(_tenantId);
        var recovered = await restartedRepository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown], "worker-b", now + 3,
            now + TimeSpan.FromMinutes(2).Ticks));
        Assert.NotNull(recovered);
        var actor = Guid.NewGuid();
        var decisionTemplate = Guid.NewGuid();
        var decisionTemplateVersion = Guid.NewGuid();
        recovered = await AdvanceAndReclaimAsync(
            restartedRepository, operation, recovered!,
            GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, now + 4);
        recovered = await AdvanceAndReclaimAsync(
            restartedRepository, operation, recovered,
            GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied, now + 6);
        recovered = await AdvanceAndReclaimAsync(
            restartedRepository, operation, recovered,
            GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, now + 8);
        Assert.True(await restartedRepository.AdvanceAsync(recovered!, new GlobalProductIdentityWorkflowCheckpointMutation(
            GlobalProductIdentityWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 10,
            WorkflowInstanceId: workflowInstanceId,
            DecisionKind: ProductIdentityDecisionKind.Approved,
            DecisionObservedAtUtcTicksV1: now + 10,
            DecisionActorSubjectId: actor,
            DecisionReasonCode: "APPROVED",
            DecisionObjectType: "GlobalProduct",
            DecisionObjectId: operation.GlobalProductId.ToString("D"),
            DecisionObjectRef: $"GP:{operation.GlobalProductId:D}",
            DecisionWorkflowTemplateId: decisionTemplate,
            DecisionWorkflowTemplateVersionId: decisionTemplateVersion,
            DecisionTaskStatus: "Approved",
            DecisionInstanceStatus: "Completed",
            DecisionTransitionSequence: 3,
            DecisionAtUtcTicksV1: now + 3,
            ReleaseLease: true)));

        var stored = await restartedRepository.GetByOperationIdAsync(operation.OperationId);
        Assert.NotNull(stored);
        Assert.Equal(templateVersionId, stored!.WorkflowTemplateVersionId);
        Assert.Equal(resolvedTemplateId, stored.WorkflowTemplateId);
        Assert.Equal(workflowInstanceId, stored.WorkflowInstanceId);
        Assert.Equal(actor, stored.DecisionActorSubjectId);
        Assert.Equal(decisionTemplateVersion, stored.DecisionWorkflowTemplateVersionId);
        Assert.Equal("Approved", stored.DecisionTaskStatus);
        Assert.Equal("Completed", stored.DecisionInstanceStatus);
        Assert.Equal(3, stored.DecisionTransitionSequence);
        Assert.Equal(now + 3, stored.DecisionAtUtcTicksV1);
    }

    [Fact]
    public async Task Recoverable_page_is_due_lease_safe_and_deterministically_paged_with_same_tick_tie_break()
    {
        var repository = Repository(_tenantId);
        var now = new DateTimeOffset(2026, 8, 29, 14, 0, 0, TimeSpan.Zero).UtcTicks;
        var first = Operation(_tenantId, "page-a", "fp-a", operationId: Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var second = Operation(_tenantId, "page-b", "fp-b", operationId: Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var nullDue = Operation(_tenantId, "page-null", "fp-null", operationId: Guid.Parse("01000000-0000-0000-0000-000000000001"));
        var leased = Operation(_tenantId, "page-c", "fp-c");
        var future = Operation(_tenantId, "page-d", "fp-d");
        var completed = Operation(_tenantId, "page-e", "fp-e");
        foreach (var item in new[] { first, second, nullDue, leased, future, completed })
            Assert.True((await repository.ReserveAsync(item)).Succeeded);

        await _collection.UpdateManyAsync(
            item => item.TenantId == _tenantId && new[] { first.OperationId, second.OperationId }.Contains(item.OperationId),
            Builders<GlobalProductIdentityWorkflowOperation>.Update.Set(item => item.NextAttemptAtUtcTicksV1, now));
        await _collection.UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == leased.OperationId,
            Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(item => item.NextAttemptAtUtcTicksV1, now)
                .Set(item => item.LeaseOwner, "active")
                .Set(item => item.LeaseUntilUtcTicksV1, now + TimeSpan.FromMinutes(1).Ticks));
        await _collection.UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == future.OperationId,
            Builders<GlobalProductIdentityWorkflowOperation>.Update.Set(
                item => item.NextAttemptAtUtcTicksV1, now + TimeSpan.FromMinutes(1).Ticks));
        await _collection.UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == completed.OperationId,
            Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(item => item.NextAttemptAtUtcTicksV1, now)
                .Set(item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.Completed));

        var page1 = await repository.DiscoverRecoverableAsync(now, 1);
        var page2 = await repository.DiscoverRecoverableAsync(now, 1, page1.NextCursor);
        var page3 = await repository.DiscoverRecoverableAsync(now, 1, page2.NextCursor);
        var page4 = await repository.DiscoverRecoverableAsync(now, 1, page3.NextCursor);

        Assert.Equal(nullDue.OperationId, Assert.Single(page1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(page2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(page3.Operations).OperationId);
        Assert.Empty(page4.Operations);
    }

    [Fact]
    public async Task Four_indexes_and_scalar_temporal_storage_are_exact_and_no_credentials_are_persisted()
    {
        var operation = Operation(_tenantId, "shape", "shape-fingerprint");
        Assert.True((await Repository(_tenantId).ReserveAsync(operation)).Succeeded);

        var indexes = await (await _collection.Indexes.ListAsync()).ToListAsync();
        var owned = indexes.Where(item => item["name"].AsString.StartsWith(
            "ux_mdm_gp_identity_workflow_", StringComparison.Ordinal)
            || item["name"] == "ix_mdm_gp_identity_workflow_recovery").ToArray();
        Assert.Equal(4, owned.Length);
        Assert.Equal(
            new[]
            {
                "ix_mdm_gp_identity_workflow_recovery",
                "ux_mdm_gp_identity_workflow_operation",
                "ux_mdm_gp_identity_workflow_product_version",
                "ux_mdm_gp_identity_workflow_start_key"
            },
            owned.Select(item => item["name"].AsString).OrderBy(item => item, StringComparer.Ordinal));

        var raw = await _database.GetCollection<BsonDocument>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(new BsonDocument("OperationId", new BsonBinaryData(operation.OperationId, GuidRepresentation.Standard)))
            .SingleAsync();
        Assert.True(raw["CreatedAtUtcTicksV1"].IsInt64);
        Assert.True(raw["UpdatedAtUtcTicksV1"].IsInt64);
        Assert.Equal(1, raw["TemporalStorageVersion"].AsInt32);
        var names = raw.Names.ToArray();
        Assert.DoesNotContain(names, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Credential", StringComparison.OrdinalIgnoreCase));

        var explain = await _database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["find"] = GlobalProductIdentityWorkflowOperationRepository.CollectionName,
                ["filter"] = new BsonDocument
                {
                    ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
                    ["IsDeleted"] = false,
                    ["NextAttemptAtUtcTicksV1"] = new BsonDocument("$lte", DateTimeOffset.UtcNow.UtcTicks)
                },
                ["sort"] = new BsonDocument
                {
                    ["NextAttemptAtUtcTicksV1"] = 1,
                    ["OperationId"] = 1
                },
                ["hint"] = "ix_mdm_gp_identity_workflow_recovery",
                ["limit"] = 10
            },
            ["verbosity"] = "queryPlanner"
        });
        var explainText = explain.ToJson();
        Assert.Contains("IXSCAN", explainText, StringComparison.Ordinal);
        Assert.Contains("ix_mdm_gp_identity_workflow_recovery", explainText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_partition_discovery_returns_only_due_recoverable_partitions()
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        Assert.True((await Repository(_tenantId).ReserveAsync(Operation(_tenantId, "tenant-a", "tenant-a"))).Succeeded);
        Assert.True((await Repository(_tenantBId).ReserveAsync(Operation(_tenantBId, "tenant-b", "tenant-b"))).Succeeded);
        var discovery = new GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository(_database);

        var first = await discovery.DiscoverAsync(null, now, 1);
        var second = await discovery.DiscoverAsync(first.NextAfterTenantId, now, 1);
        var tenantIds = first.TenantIds.Concat(second.TenantIds).ToArray();

        Assert.Contains(_tenantId, tenantIds);
        Assert.Contains(_tenantBId, tenantIds);
        Assert.Equal(2, tenantIds.Distinct().Count());
        Assert.Single(first.TenantIds);
        Assert.Single(second.TenantIds);
    }

    public async Task DisposeAsync() => await _collection.DeleteManyAsync(
        item => item.TenantId == _tenantId || item.TenantId == _tenantBId);

    private static async Task<GlobalProductIdentityWorkflowClaim> AdvanceAndReclaimAsync(
        GlobalProductIdentityWorkflowOperationRepository repository,
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint next,
        long nowUtcTicks)
    {
        Assert.True(await repository.AdvanceAsync(claim, new GlobalProductIdentityWorkflowCheckpointMutation(
            next,
            ProductIdentityWorkflowRecoveryDisposition.None,
            nowUtcTicks,
            ReleaseLease: true)));
        var nextClaim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [next],
            $"worker-{(int)next}",
            nowUtcTicks + 1,
            nowUtcTicks + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(nextClaim);
        return nextClaim!;
    }

    private GlobalProductIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private static GlobalProductIdentityWorkflowOperation Operation(
        Guid tenantId,
        string key,
        string fingerprint,
        Guid? operationId = null,
        Guid? productId = null,
        int expectedVersion = 0)
    {
        var resolvedProductId = productId ?? Guid.NewGuid();
        return new()
        {
            TenantId = tenantId,
            OperationId = operationId ?? Guid.NewGuid(),
            GlobalProductId = resolvedProductId,
            ExpectedProductVersion = expectedVersion,
            MakerSubjectId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "GLOBAL_PRODUCT_IDENTITY_APPROVAL",
            CommentRequired = true,
            EvidenceRequired = true,
            ObjectType = "GlobalProduct",
            ObjectId = resolvedProductId.ToString("D"),
            ObjectRef = $"GP:{resolvedProductId:D}",
            StartIdempotencyKey = key,
            OperationFingerprint = fingerprint,
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
