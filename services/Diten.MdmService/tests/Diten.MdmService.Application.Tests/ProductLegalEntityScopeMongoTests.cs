using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeMongoTests
{
    [Fact]
    public async Task Repository_indexes_are_exact_and_create_replay_is_idempotent_while_payload_drift_conflicts()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.PolicyRepository();
        var commandId = Guid.NewGuid();
        var policy = CreatePolicy(scope.TenantId, Guid.NewGuid(), commandId);

        var created = await repository.CreateAsync(policy);
        var replay = await repository.CreateAsync(policy);
        var drift = CreatePolicy(scope.TenantId, Guid.NewGuid(), commandId);
        var conflict = await repository.CreateAsync(drift);
        var indexes = await scope.PolicyCollection.Indexes.ListAsync();
        var indexDocuments = await indexes.ToListAsync();

        Assert.True(created.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(created.Policy!.Id, replay.Policy!.Id);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.VersionConflict);
        Assert.False(conflict.WriteOutcomeAmbiguous);
        Assert.Equal(
            [
                "_id_",
                "ix_mdm_product_legal_entity_scope_policies_tenant_audit_delivery",
                "ux_mdm_product_legal_entity_scope_policies_tenant_command",
                "ux_mdm_product_legal_entity_scope_policies_tenant_product"
            ],
            indexDocuments.Select(index => index["name"].AsString).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Concurrent_compare_and_swap_has_one_winner_and_preserves_one_current_period()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.PolicyRepository();
        var productId = Guid.NewGuid();
        var initial = CreatePolicy(scope.TenantId, productId, Guid.NewGuid());
        Assert.True((await repository.CreateAsync(initial)).Succeeded);
        var first = await repository.GetByGlobalProductIdAsync(productId);
        var second = await repository.GetByGlobalProductIdAsync(productId);
        var now = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        first!.ReplaceCurrent(0, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped, [Guid.NewGuid()], Guid.NewGuid(), now);
        AddPolicyUpdateAuditIntent(first, ProductAuditOperation.ProductLegalEntityScopePolicyReplaced, 0);
        second!.ReplaceCurrent(0, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped, [Guid.NewGuid()], Guid.NewGuid(), now);
        AddPolicyUpdateAuditIntent(second, ProductAuditOperation.ProductLegalEntityScopePolicyReplaced, 0);

        var results = await Task.WhenAll(
            repository.UpdateAsync(first, 0),
            repository.UpdateAsync(second, 0));
        var stored = await repository.GetByGlobalProductIdAsync(productId);

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => result.VersionConflict));
        Assert.Equal(1, stored!.Version);
        Assert.Equal(2, stored.ScopePeriods.Count);
        Assert.Single(stored.ScopePeriods, period => period.EffectiveToUtc is null);
    }

    [Fact]
    public async Task Audit_lifecycle_headroom_boundary_accepts_exact_ceiling_and_rejects_one_byte_over_without_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.PolicyRepository();
        const int auditLifecycleHeadroomBytes = 64 * 1024;
        const int persistedBusinessCeiling =
            ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - auditLifecycleHeadroomBytes;
        var exact = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var intent = Assert.Single(exact.AuditIntents);
        intent.EvidenceHash = string.Empty;
        var emptySize = exact.ToBsonDocument().ToBson().Length;
        intent.EvidenceHash = new string('x', persistedBusinessCeiling - emptySize);
        Assert.Equal(
            persistedBusinessCeiling,
            exact.ToBsonDocument().ToBson().Length);

        var accepted = await repository.CreateAsync(exact);
        var oversized = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var oversizedIntent = Assert.Single(oversized.AuditIntents);
        oversizedIntent.EvidenceHash = string.Empty;
        var oversizedBase = oversized.ToBsonDocument().ToBson().Length;
        oversizedIntent.EvidenceHash = new string(
            'x',
            persistedBusinessCeiling - oversizedBase + 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(oversized));
        Assert.Null(await repository.GetByGlobalProductIdAsync(oversized.GlobalProductId));
        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task Tenant_soft_delete_no_reuse_and_cancellation_fail_closed()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var productId = Guid.NewGuid();
        var policy = CreatePolicy(scope.TenantId, productId, Guid.NewGuid());
        Assert.True((await scope.PolicyRepository().CreateAsync(policy)).Succeeded);

        var otherTenantRepository = scope.PolicyRepository(Guid.NewGuid());
        Assert.Null(await otherTenantRepository.GetByGlobalProductIdAsync(productId));
        await scope.PolicyCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == policy.Id,
            Builders<ProductLegalEntityScopePolicy>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        Assert.Null(await scope.PolicyRepository().GetByGlobalProductIdAsync(productId));

        var reused = CreatePolicy(scope.TenantId, productId, Guid.NewGuid());
        var noReuse = await scope.PolicyRepository().CreateAsync(reused);
        Assert.False(noReuse.Succeeded);
        Assert.True(noReuse.VersionConflict);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.PolicyRepository().CreateAsync(
                CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()),
                cancelled.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Whitelisted_ambiguous_create_uses_exact_reread_without_false_success(int exceptionKind)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var beforeWrite = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var beforeRepository = scope.PolicyRepositoryWithInsertExecutor(
            _ => Task.FromException(CreateAmbiguousException(exceptionKind)));

        var ambiguous = await beforeRepository.CreateAsync(beforeWrite);

        Assert.False(ambiguous.Succeeded);
        Assert.True(ambiguous.WriteOutcomeAmbiguous);
        Assert.False(ambiguous.VersionConflict);
        Assert.Null(await scope.PolicyRepository().GetByGlobalProductIdAsync(beforeWrite.GlobalProductId));

        var afterWrite = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var afterRepository = scope.PolicyRepositoryWithInsertExecutor(async operation =>
        {
            await operation();
            throw CreateAmbiguousException(exceptionKind);
        });

        var recovered = await afterRepository.CreateAsync(afterWrite);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        Assert.Equal(
            afterWrite.Id,
            (await scope.PolicyRepository().GetByGlobalProductIdAsync(afterWrite.GlobalProductId))!.Id);

        var commandId = Guid.NewGuid();
        var existing = CreatePolicy(scope.TenantId, Guid.NewGuid(), commandId);
        Assert.True((await scope.PolicyRepository().CreateAsync(existing)).Succeeded);
        var drift = CreatePolicy(scope.TenantId, Guid.NewGuid(), commandId);
        var mismatchRepository = scope.PolicyRepositoryWithInsertExecutor(
            _ => Task.FromException(CreateAmbiguousException(exceptionKind)));

        var mismatch = await mismatchRepository.CreateAsync(drift);

        Assert.False(mismatch.Succeeded);
        Assert.True(mismatch.WriteOutcomeAmbiguous);
        Assert.False(mismatch.VersionConflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Whitelisted_ambiguous_update_uses_exact_reread_without_false_success(int exceptionKind)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var productId = Guid.NewGuid();
        Assert.True((await scope.PolicyRepository().CreateAsync(
            CreatePolicy(scope.TenantId, productId, Guid.NewGuid()))).Succeeded);
        var requested = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);
        requested!.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [Guid.NewGuid()],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMilliseconds(-1));
        AddPolicyUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            0);
        var beforeRepository = scope.PolicyRepositoryWithExecutors(
            operation => operation(),
            _ => Task.FromException<ProductLegalEntityScopePolicy?>(CreateAmbiguousException(exceptionKind)));

        var ambiguous = await beforeRepository.UpdateAsync(requested, 0);
        var unchanged = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);

        Assert.False(ambiguous.Succeeded);
        Assert.True(ambiguous.WriteOutcomeAmbiguous);
        Assert.Equal(0, unchanged!.Version);
        Assert.Single(unchanged.ScopePeriods);

        var afterRepository = scope.PolicyRepositoryWithExecutors(
            operation => operation(),
            async operation =>
            {
                _ = await operation();
                throw CreateAmbiguousException(exceptionKind);
            });

        var recovered = await afterRepository.UpdateAsync(requested, 0);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        Assert.Equal(1, (await scope.PolicyRepository().GetByGlobalProductIdAsync(productId))!.Version);
    }

    [Theory]
    [InlineData("aggregate-type")]
    [InlineData("source-service")]
    [InlineData("operation")]
    [InlineData("version")]
    public async Task Invalid_policy_create_audit_binding_is_rejected_without_business_or_audit_mutation(string mismatch)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var policy = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var intent = Assert.Single(policy.AuditIntents);
        switch (mismatch)
        {
            case "aggregate-type":
                intent.AggregateType = AuditAggregateType.GlobalProduct;
                break;
            case "source-service":
                intent.SourceService = "forged-service";
                break;
            case "operation":
                intent.Operation = ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated;
                break;
            case "version":
                intent.PreVersion = 0;
                intent.PostVersion = 1;
                break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => scope.PolicyRepository().CreateAsync(policy));
        Assert.Equal(0, await scope.PolicyCollection.CountDocumentsAsync(item => item.TenantId == scope.TenantId));
    }

    [Fact]
    public async Task Missing_policy_create_or_update_audit_intent_is_rejected_without_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var missingCreate = CreatePolicy(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        missingCreate.AuditIntents.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => scope.PolicyRepository().CreateAsync(missingCreate));
        Assert.Equal(0, await scope.PolicyCollection.CountDocumentsAsync(item => item.TenantId == scope.TenantId));

        var productId = Guid.NewGuid();
        Assert.True((await scope.PolicyRepository().CreateAsync(
            CreatePolicy(scope.TenantId, productId, Guid.NewGuid()))).Succeeded);
        var requested = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);
        requested!.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [Guid.NewGuid()],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMilliseconds(-1));

        await Assert.ThrowsAsync<ArgumentException>(() => scope.PolicyRepository().UpdateAsync(requested, 0));
        var stored = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);
        Assert.Equal(0, stored!.Version);
        Assert.Single(stored.ScopePeriods);
        Assert.Single(stored.AuditIntents);
    }

    [Fact]
    public async Task Existing_policy_audit_intent_payload_drift_is_rejected_without_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var productId = Guid.NewGuid();
        Assert.True((await scope.PolicyRepository().CreateAsync(
            CreatePolicy(scope.TenantId, productId, Guid.NewGuid()))).Succeeded);
        var requested = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);
        var originalEvidence = Assert.Single(requested!.AuditIntents).EvidenceHash;
        requested.AuditIntents[0].EvidenceHash = "immutable-drift";
        requested.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [Guid.NewGuid()],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMilliseconds(-1));
        AddPolicyUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.PolicyRepository().UpdateAsync(requested, 0));
        var stored = await scope.PolicyRepository().GetByGlobalProductIdAsync(productId);
        Assert.Equal(0, stored!.Version);
        Assert.Single(stored.ScopePeriods);
        Assert.Equal(originalEvidence, Assert.Single(stored.AuditIntents).EvidenceHash);
    }

    [Fact]
    public void Audit_enum_values_remain_append_only_and_numeric_compatible()
    {
        Assert.Equal(1, (int)AuditAggregateType.CodeReservation);
        Assert.Equal(6, (int)AuditAggregateType.Lsku);
        Assert.Equal(7, (int)AuditAggregateType.ProductLegalEntityScopePolicy);
        Assert.Equal(8, (int)AuditAggregateType.ProductLegalEntityScopeRolloutState);
        Assert.Equal(1, (int)ProductAuditOperation.CodeReserved);
        Assert.Equal(10, (int)ProductAuditOperation.LskuDraftCreated);
        Assert.Equal(11, (int)ProductAuditOperation.ProductLegalEntityScopePolicyCreated);
        Assert.Equal(12, (int)ProductAuditOperation.ProductLegalEntityScopePolicyReplaced);
        Assert.Equal(13, (int)ProductAuditOperation.ProductLegalEntityScopePolicyEnded);
        Assert.Equal(14, (int)ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated);
        Assert.Equal(15, (int)ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended);
    }

    internal static ProductLegalEntityScopePolicy CreatePolicy(
        Guid tenantId,
        Guid productId,
        Guid commandId)
    {
        var policy = ProductLegalEntityScopePolicy.Create(
            tenantId,
            productId,
            commandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddSeconds(-5));
        var period = Assert.Single(policy.ScopePeriods);
        policy.AuditIntents =
        [
            CreateBoundAuditIntent(
                tenantId,
                AuditAggregateType.ProductLegalEntityScopePolicy,
                policy.Id,
                ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
                -1,
                0,
                period.CommandId,
                period.ActorId,
                period.CreatedAtUtc)
        ];
        return policy;
    }

    internal static void AddPolicyUpdateAuditIntent(
        ProductLegalEntityScopePolicy policy,
        ProductAuditOperation operation,
        int preVersion)
    {
        var period = policy.ScopePeriods[^1];
        var isEnd = operation == ProductAuditOperation.ProductLegalEntityScopePolicyEnded;
        policy.AuditIntents.Add(CreateBoundAuditIntent(
            policy.TenantId,
            AuditAggregateType.ProductLegalEntityScopePolicy,
            policy.Id,
            operation,
            preVersion,
            preVersion + 1,
            isEnd ? period.EndCommandId!.Value : period.CommandId,
            isEnd ? period.EndedByActorId!.Value : period.ActorId,
            isEnd ? period.EndedAtUtc!.Value : period.CreatedAtUtc));
    }

    internal static LocalAuditIntent CreateBoundAuditIntent(
        Guid tenantId,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        ProductAuditOperation operation,
        int preVersion,
        int postVersion,
        Guid commandId,
        Guid actorId,
        DateTimeOffset timestampUtc) => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Operation = operation,
            PreVersion = preVersion,
            PostVersion = postVersion,
            ActorId = actorId.ToString("D"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = Guid.NewGuid().ToString("N"),
            CommandId = commandId.ToString("D"),
            Sequence = postVersion + 1L,
            TimestampUtc = timestampUtc,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            EvidenceHash = Guid.NewGuid().ToString("N"),
            SnapshotReference = "product-scope-test-snapshot",
            DeliveryState = AuditIntentDeliveryState.Pending
        };

    internal static LocalAuditIntent CreateAuditIntent(
        Guid tenantId,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        ProductAuditOperation operation) => new()
        {
            IntentId = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Operation = operation,
            ActorId = Guid.NewGuid().ToString("N"),
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = Guid.NewGuid().ToString("N"),
            CommandId = Guid.NewGuid().ToString("N"),
            Sequence = 1,
            TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            EvidenceHash = string.Empty,
            DeliveryState = AuditIntentDeliveryState.Pending
        };

    internal static Exception CreateAmbiguousException(int exceptionKind)
    {
        var type = exceptionKind switch
        {
            0 => typeof(MongoConnectionException),
            1 => typeof(MongoExecutionTimeoutException),
            2 => typeof(MongoWriteConcernException),
            _ => throw new ArgumentOutOfRangeException(nameof(exceptionKind))
        };
        return (Exception)RuntimeHelpers.GetUninitializedObject(type);
    }
}

internal sealed class ProductScopeMongoScope : IAsyncDisposable
{
    private ProductScopeMongoScope(IMongoDatabase database)
    {
        Database = database;
    }

    public Guid TenantId { get; } = Guid.NewGuid();
    public IMongoDatabase Database { get; }
    public IMongoCollection<ProductLegalEntityScopePolicy> PolicyCollection =>
        Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies");
    public IMongoCollection<ProductLegalEntityScopeRolloutState> RolloutCollection =>
        Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states");

    public static async Task<ProductScopeMongoScope> CreateAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017";
        var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var database = new MongoClient(settings)
            .GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        return new ProductScopeMongoScope(database);
    }

    public ProductLegalEntityScopePolicyRepository PolicyRepository(Guid? tenantId = null) =>
        new(Database, new TenantContext(tenantId ?? TenantId));

    public ProductLegalEntityScopePolicyRepository PolicyRepositoryWithInsertExecutor(
        Func<Func<Task>, Task> insertExecutor)
        => PolicyRepositoryWithExecutors(
            insertExecutor,
            operation => operation());

    public ProductLegalEntityScopePolicyRepository PolicyRepositoryWithExecutors(
        Func<Func<Task>, Task> insertExecutor,
        Func<Func<Task<ProductLegalEntityScopePolicy?>>, Task<ProductLegalEntityScopePolicy?>> updateExecutor)
    {
        var constructor = typeof(ProductLegalEntityScopePolicyRepository)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 4);
        return (ProductLegalEntityScopePolicyRepository)constructor.Invoke(
        [
            Database,
            new TenantContext(TenantId),
            insertExecutor,
            updateExecutor
        ]);
    }

    public ProductLegalEntityScopeRolloutStateRepository RolloutRepository(Guid? tenantId = null) =>
        new(Database, new TenantContext(tenantId ?? TenantId));

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            PolicyCollection.DeleteManyAsync(item => item.TenantId == TenantId),
            RolloutCollection.DeleteManyAsync(item => item.TenantId == TenantId));
    }

    internal sealed class TenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
