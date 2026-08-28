using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Reflection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeRolloutMongoTests
{
    [Fact]
    public async Task Malformed_persisted_rollout_fails_closed_for_get_lease_and_fence_without_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == state.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                item => item.Mode,
                (ProductLegalEntityScopeRolloutMode)999));

        var leaseAcquiredAt = DateTimeOffset.UtcNow;
        var lease = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(),
            Generation = 1,
            CommandId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            MutationKind = "CreateGlobalProductDraft",
            PayloadFingerprint = new string('A', 64),
            Owner = "Diten.MDM:CreateGlobalProductDraft",
            AcquiredAtUtc = leaseAcquiredAt,
            ExpiresAtUtc = leaseAcquiredAt.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds)
        };
        var fence = new ProductLegalEntityScopeActivationFence
        {
            Token = new string('A', 64),
            State = ProductLegalEntityScopeAdmissionState.Closing,
            Action = "ActivateEnforced",
            CommandId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            ReasonCode = "OWNER_APPROVED",
            AcquiredAtUtc = DateTimeOffset.UtcNow
        };

        async Task AssertMalformedAsync(Func<Task> action)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
            Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_STATE_INVALID", exception.Message);
        }

        await AssertMalformedAsync(async () => await repository.GetAsync());
        await AssertMalformedAsync(async () => await repository.GetByCreationCommandIdAsync(state.CreationCommandId));
        await AssertMalformedAsync(async () => await repository.AcquireWriterLeaseAsync(lease));
        await AssertMalformedAsync(async () => await repository.AcquireFenceAsync(
            fence,
            state.Id,
            0,
            ProductLegalEntityScopeRolloutMode.Preparation));

        var persisted = await scope.RolloutCollection
            .Find(item => item.TenantId == scope.TenantId && item.Id == state.Id)
            .SingleAsync();
        Assert.Equal((ProductLegalEntityScopeRolloutMode)999, persisted.Mode);
        Assert.Null(persisted.ActiveWriterLease);
        Assert.Null(persisted.ActiveFence);
    }

    [Fact]
    public async Task Repository_indexes_are_exact_and_create_replay_is_idempotent_while_payload_drift_conflicts()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var commandId = Guid.NewGuid();
        var state = CreateState(scope.TenantId, commandId, Guid.NewGuid());

        var created = await repository.CreateAsync(state);
        var replay = await repository.CreateAsync(state);
        var drift = CreateState(scope.TenantId, commandId, Guid.NewGuid());
        var conflict = await repository.CreateAsync(drift);
        var indexes = await scope.RolloutCollection.Indexes.ListAsync();
        var indexDocuments = await indexes.ToListAsync();

        Assert.True(created.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(created.State!.Id, replay.State!.Id);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.VersionConflict);
        Assert.False(conflict.WriteOutcomeAmbiguous);
        Assert.Equal(
            [
                "_id_",
                "ix_mdm_product_legal_entity_scope_rollout_states_tenant_audit_delivery",
                "ux_mdm_product_legal_entity_scope_rollout_states_tenant",
                "ux_mdm_product_legal_entity_scope_rollout_states_tenant_command"
            ],
            indexDocuments.Select(index => index["name"].AsString).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Concurrent_compare_and_swap_has_one_winner_without_whole_document_replacement()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        var first = await repository.GetAsync();
        var second = await repository.GetAsync();
        first!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        first!.Version = 1;
        first.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(first, ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated, 0);
        second!.Mode = ProductLegalEntityScopeRolloutMode.FailClosedSuspended;
        second!.Version = 1;
        second.UpdatedAt = first.UpdatedAt.Value.AddTicks(1);
        AddRolloutUpdateAuditIntent(second, ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended, 0);

        var results = await Task.WhenAll(
            repository.UpdateAsync(first, 0),
            repository.UpdateAsync(second, 0));
        var stored = await repository.GetAsync();

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => result.VersionConflict));
        Assert.Equal(1, stored!.Version);
        Assert.Contains(
            stored.Mode,
            new[]
            {
                ProductLegalEntityScopeRolloutMode.Enforced,
                ProductLegalEntityScopeRolloutMode.FailClosedSuspended
            });
    }

    [Fact]
    public async Task Tenant_soft_delete_no_reuse_and_cancellation_fail_closed()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var commandId = Guid.NewGuid();
        var state = CreateState(scope.TenantId, commandId, Guid.NewGuid());
        Assert.True((await scope.RolloutRepository().CreateAsync(state)).Succeeded);

        Assert.Null(await scope.RolloutRepository(Guid.NewGuid()).GetAsync());
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == state.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        Assert.Null(await scope.RolloutRepository().GetAsync());

        var noReuse = await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(noReuse.Succeeded);
        Assert.True(noReuse.VersionConflict);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.RolloutRepository().CreateAsync(
                CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()),
                cancelled.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Whitelisted_ambiguous_create_uses_exact_reread_without_false_success(int exceptionKind)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var beforeWrite = CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var beforeRepository = RolloutRepositoryWithInsertExecutor(
            scope,
            _ => Task.FromException(ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind)));

        var ambiguous = await beforeRepository.CreateAsync(beforeWrite);

        Assert.False(ambiguous.Succeeded);
        Assert.True(ambiguous.WriteOutcomeAmbiguous);
        Assert.False(ambiguous.VersionConflict);
        Assert.Null(await scope.RolloutRepository().GetAsync());

        var afterWrite = CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        var afterRepository = RolloutRepositoryWithInsertExecutor(scope, async operation =>
        {
            await operation();
            throw ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind);
        });

        var recovered = await afterRepository.CreateAsync(afterWrite);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        Assert.Equal(afterWrite.Id, (await scope.RolloutRepository().GetAsync())!.Id);

        await scope.RolloutCollection.DeleteManyAsync(item => item.TenantId == scope.TenantId);
        var commandId = Guid.NewGuid();
        var existing = CreateState(scope.TenantId, commandId, Guid.NewGuid());
        Assert.True((await scope.RolloutRepository().CreateAsync(existing)).Succeeded);
        var drift = CreateState(scope.TenantId, commandId, Guid.NewGuid());
        var mismatchRepository = RolloutRepositoryWithInsertExecutor(
            scope,
            _ => Task.FromException(ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind)));

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
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var requested = await scope.RolloutRepository().GetAsync();
        requested!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        requested.Version = 1;
        requested.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        var beforeRepository = RolloutRepositoryWithExecutors(
            scope,
            operation => operation(),
            _ => Task.FromException<ProductLegalEntityScopeRolloutState?>(
                ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind)));

        var ambiguous = await beforeRepository.UpdateAsync(requested, 0);
        var unchanged = await scope.RolloutRepository().GetAsync();

        Assert.False(ambiguous.Succeeded);
        Assert.True(ambiguous.WriteOutcomeAmbiguous);
        Assert.Equal(0, unchanged!.Version);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, unchanged.Mode);

        var afterRepository = RolloutRepositoryWithExecutors(
            scope,
            operation => operation(),
            async operation =>
            {
                _ = await operation();
                throw ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind);
            });

        var recovered = await afterRepository.UpdateAsync(requested, 0);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        var stored = await scope.RolloutRepository().GetAsync();
        Assert.Equal(1, stored!.Version);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, stored.Mode);
    }

    [Theory]
    [InlineData("aggregate-type")]
    [InlineData("source-service")]
    [InlineData("operation")]
    [InlineData("version")]
    public async Task Invalid_rollout_update_audit_binding_is_rejected_without_business_or_audit_mutation(
        string mismatch)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var requested = await scope.RolloutRepository().GetAsync();
        requested!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        requested.Version = 1;
        requested.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        var intent = Assert.Single(requested.AuditIntents);
        switch (mismatch)
        {
            case "aggregate-type":
                intent.AggregateType = AuditAggregateType.GlobalProduct;
                break;
            case "source-service":
                intent.SourceService = "forged-service";
                break;
            case "operation":
                intent.Operation = ProductAuditOperation.ProductLegalEntityScopePolicyReplaced;
                break;
            case "version":
                intent.PreVersion = 1;
                intent.PostVersion = 2;
                break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => scope.RolloutRepository().UpdateAsync(requested, 0));
        var stored = await scope.RolloutRepository().GetAsync();
        Assert.Equal(0, stored!.Version);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, stored.Mode);
        Assert.Empty(stored.AuditIntents);
    }

    [Fact]
    public async Task Preparation_create_rejects_supplied_audit_and_rollout_update_requires_one_intent()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var invalidCreate = CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid());
        invalidCreate.AuditIntents.Add(ProductLegalEntityScopeMongoTests.CreateBoundAuditIntent(
            scope.TenantId,
            AuditAggregateType.ProductLegalEntityScopeRolloutState,
            invalidCreate.Id,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            -1,
            0,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddSeconds(-1)));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.RolloutRepository().CreateAsync(invalidCreate));
        Assert.Equal(0, await scope.RolloutCollection.CountDocumentsAsync(item => item.TenantId == scope.TenantId));

        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var missingUpdate = await scope.RolloutRepository().GetAsync();
        missingUpdate!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        missingUpdate.Version = 1;
        missingUpdate.UpdatedAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<ArgumentException>(() => scope.RolloutRepository().UpdateAsync(missingUpdate, 0));
        var stored = await scope.RolloutRepository().GetAsync();
        Assert.Equal(0, stored!.Version);
        Assert.Empty(stored.AuditIntents);
    }

    [Fact]
    public async Task Existing_rollout_audit_intent_payload_drift_is_rejected_without_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var activated = await scope.RolloutRepository().GetAsync();
        activated!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        activated.Version = 1;
        activated.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            activated,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        Assert.True((await scope.RolloutRepository().UpdateAsync(activated, 0)).Succeeded);

        var requested = await scope.RolloutRepository().GetAsync();
        var originalEvidence = Assert.Single(requested!.AuditIntents).EvidenceHash;
        requested.AuditIntents[0].EvidenceHash = "immutable-drift";
        requested.Mode = ProductLegalEntityScopeRolloutMode.FailClosedSuspended;
        requested.Version = 2;
        requested.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended,
            1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RolloutRepository().UpdateAsync(requested, 1));
        var stored = await scope.RolloutRepository().GetAsync();
        Assert.Equal(1, stored!.Version);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, stored.Mode);
        Assert.Equal(originalEvidence, Assert.Single(stored.AuditIntents).EvidenceHash);
    }

    [Fact]
    public async Task Audit_lifecycle_headroom_boundary_is_enforced_for_rollout_update()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        const int persistedBusinessCeiling =
            ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - 64 * 1024;
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var exact = await scope.RolloutRepository().GetAsync();
        exact!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        exact.Version = 1;
        exact.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            exact,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        var exactIntent = Assert.Single(exact.AuditIntents);
        exactIntent.EvidenceHash = string.Empty;
        var baseSize = exact.ToBsonDocument().ToBson().Length;
        exactIntent.EvidenceHash = new string('x', persistedBusinessCeiling - baseSize);
        Assert.Equal(persistedBusinessCeiling, exact.ToBsonDocument().ToBson().Length);
        Assert.True((await scope.RolloutRepository().UpdateAsync(exact, 0)).Succeeded);

        await scope.RolloutCollection.DeleteManyAsync(item => item.TenantId == scope.TenantId);
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var oversized = await scope.RolloutRepository().GetAsync();
        oversized!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        oversized.Version = 1;
        oversized.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            oversized,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        var oversizedIntent = Assert.Single(oversized.AuditIntents);
        oversizedIntent.EvidenceHash = string.Empty;
        var oversizedBase = oversized.ToBsonDocument().ToBson().Length;
        oversizedIntent.EvidenceHash = new string('x', persistedBusinessCeiling - oversizedBase + 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RolloutRepository().UpdateAsync(oversized, 0));
        var unchanged = await scope.RolloutRepository().GetAsync();
        Assert.Equal(0, unchanged!.Version);
        Assert.Empty(unchanged.AuditIntents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Ambiguous_rollout_recovery_requires_exact_command_and_audit_proof(int exceptionKind)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        Assert.True((await scope.RolloutRepository().CreateAsync(
            CreateState(scope.TenantId, Guid.NewGuid(), Guid.NewGuid()))).Succeeded);
        var competing = await scope.RolloutRepository().GetAsync();
        var requested = await scope.RolloutRepository().GetAsync();
        competing!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        competing.Version = 1;
        competing.UpdatedAt = DateTimeOffset.UtcNow;
        AddRolloutUpdateAuditIntent(
            competing,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        requested!.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        requested.Version = 1;
        requested.UpdatedAt = competing.UpdatedAt;
        AddRolloutUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0);
        var faulting = RolloutRepositoryWithExecutors(
            scope,
            operation => operation(),
            async _ =>
            {
                await scope.RolloutCollection.UpdateOneAsync(
                    item => item.TenantId == scope.TenantId && item.Id == competing.Id && item.Version == 0,
                    Builders<ProductLegalEntityScopeRolloutState>.Update
                        .Set(item => item.Mode, competing.Mode)
                        .Set(item => item.Version, competing.Version)
                        .Set(item => item.UpdatedAt, competing.UpdatedAt)
                        .PushEach(item => item.AuditIntents, competing.AuditIntents));
                throw ProductLegalEntityScopeMongoTests.CreateAmbiguousException(exceptionKind);
            });

        var result = await faulting.UpdateAsync(requested, 0);

        Assert.False(result.Succeeded);
        Assert.True(result.WriteOutcomeAmbiguous);
        var stored = await scope.RolloutRepository().GetAsync();
        Assert.Equal(competing.AuditIntents[0].CommandId, Assert.Single(stored!.AuditIntents).CommandId);
    }

    private static ProductLegalEntityScopeRolloutStateRepository RolloutRepositoryWithInsertExecutor(
        ProductScopeMongoScope scope,
        Func<Func<Task>, Task> insertExecutor)
        => RolloutRepositoryWithExecutors(
            scope,
            insertExecutor,
            operation => operation());

    private static ProductLegalEntityScopeRolloutStateRepository RolloutRepositoryWithExecutors(
        ProductScopeMongoScope scope,
        Func<Func<Task>, Task> insertExecutor,
        Func<Func<Task<ProductLegalEntityScopeRolloutState?>>, Task<ProductLegalEntityScopeRolloutState?>> updateExecutor)
    {
        var constructor = typeof(ProductLegalEntityScopeRolloutStateRepository)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 4);
        return (ProductLegalEntityScopeRolloutStateRepository)constructor.Invoke(
        [
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            insertExecutor,
            updateExecutor
        ]);
    }

    private static ProductLegalEntityScopeRolloutState CreateState(
        Guid tenantId,
        Guid commandId,
        Guid actorId) => ProductLegalEntityScopeRolloutState.CreatePreparation(
            tenantId,
            commandId,
            actorId,
            DateTimeOffset.UtcNow.AddSeconds(-5));

    private static void AddRolloutUpdateAuditIntent(
        ProductLegalEntityScopeRolloutState state,
        ProductAuditOperation operation,
        int preVersion)
    {
        state.AuditIntents.Add(ProductLegalEntityScopeMongoTests.CreateBoundAuditIntent(
            state.TenantId,
            AuditAggregateType.ProductLegalEntityScopeRolloutState,
            state.Id,
            operation,
            preVersion,
            preVersion + 1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMilliseconds(-1)));
    }
}
