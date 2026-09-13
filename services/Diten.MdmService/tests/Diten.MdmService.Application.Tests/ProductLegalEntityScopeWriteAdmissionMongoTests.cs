using System.Security.Claims;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Application.Tests.Audit;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeWriteAdmissionMongoTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeWriteAdmissionMongoTests(AuditIntentTemporalMongoFixture fixture)
    {
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() =>
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    [Fact]
    public async Task GuardedWrite_ExactHumanLeaseAndPreparation_AtomicallyReplacesAndPreservesUnknownBson()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set("FutureA0Field", "preserve"));
        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.True(
            result.Succeeded,
            $"guarded result conflict={result.VersionConflict}, ambiguous={result.WriteOutcomeAmbiguous}, zero={result.VerifiedZeroMutation}");
        Assert.False(result.WriteOutcomeAmbiguous);
        Assert.Equal(1, result.Policy!.Version);
        Assert.Equal(2, result.Policy.ScopePeriods.Count);
        Assert.Equal(2, result.Policy.AuditIntents.Count);
        var persisted = await rawPolicies.Find(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id)).SingleAsync();
        Assert.Equal("preserve", persisted["FutureA0Field"].AsString);
        Assert.Equal(1, persisted[nameof(ProductLegalEntityScopePolicy.Version)].AsInt32);
        Assert.Equal(2, persisted[nameof(ProductLegalEntityScopePolicy.AuditIntents)].AsBsonArray.Count);
        var rollout = await scope.RolloutRepository().GetAsync();
        Assert.NotNull(rollout!.ActiveWriterLease);
        Assert.True(await scope.RolloutRepository().ReleaseWriterLeaseAsync(
            scenario.Lease.Token,
            scenario.Lease.Generation));
    }

    [Fact]
    public async Task GuardedWrite_CommitResponseLostAfterRealCommit_RecoversExactReadbackAndReplayCannotDuplicate()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(-119));
        var commitAttempts = 0;
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            async (session, cancellationToken) =>
            {
                commitAttempts++;
                await session.CommitTransactionAsync(cancellationToken);
                var remaining = scenario.Lease.ExpiresAtUtc - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining.Add(TimeSpan.FromMilliseconds(100)), cancellationToken);
                throw new ProductLegalEntityScopeCommitResponseLostException();
            });

        var recovered = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        Assert.True(scenario.Lease.ExpiresAtUtc < DateTimeOffset.UtcNow);
        Assert.Equal(1, commitAttempts);
        var persistedBeforeReplay = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(1, persistedBeforeReplay.Version);
        Assert.Equal(2, persistedBeforeReplay.ScopePeriods.Count);
        Assert.Equal(2, persistedBeforeReplay.AuditIntents.Count);

        var replay = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(replay.Succeeded);
        Assert.True(replay.VerifiedZeroMutation);
        Assert.Equal(1, commitAttempts);
        var persistedAfterReplay = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(persistedBeforeReplay.Version, persistedAfterReplay.Version);
        Assert.Equal(
            persistedBeforeReplay.ScopePeriods.Count,
            persistedAfterReplay.ScopePeriods.Count);
        Assert.Equal(
            persistedBeforeReplay.AuditIntents.Count,
            persistedAfterReplay.AuditIntents.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("drift")]
    public async Task GuardedWrite_CommitResponseLostWithoutExactOutcome_RemainsAmbiguousAndRetainsLease(
        string outcome)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            async (session, cancellationToken) =>
            {
                await session.CommitTransactionAsync(cancellationToken);
                var filter = Builders<BsonDocument>.Filter.Eq(
                    "_id",
                    scenario.Initial.Id);
                if (outcome == "missing")
                    await rawPolicies.DeleteOneAsync(filter, cancellationToken);
                else
                    await rawPolicies.UpdateOneAsync(
                        filter,
                        Builders<BsonDocument>.Update.Set(
                            nameof(ProductLegalEntityScopePolicy.Version),
                            99),
                        cancellationToken: cancellationToken);
                throw new ProductLegalEntityScopeCommitResponseLostException();
            });

        var result = await guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.WriteOutcomeAmbiguous);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease!.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease.Generation);
    }

    [Fact]
    public async Task WriterLease_InitiallyExpiredExactIdentity_UnappliedReplaceFailsWithZeroEffectsAndRetainsLease()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(
                -ProductLegalEntityScopeWriterLease.DurationSeconds - 1));

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());

        Assert.True(
            !result.Succeeded
            && result.VerifiedZeroMutation
            && result.LeaseRetentionRequired
            && persisted.Version == 0
            && persisted.ScopePeriods.Count == 1
            && persisted.AuditIntents.Count == 1
            && rollout.ActiveWriterLease?.Token == scenario.Lease.Token
            && rollout.ActiveWriterLease.Generation == scenario.Lease.Generation,
            $"expired exact lease outcome: succeeded={result.Succeeded}, " +
            $"zero={result.VerifiedZeroMutation}, version={persisted.Version}, " +
            $"periods={persisted.ScopePeriods.Count}, audits={persisted.AuditIntents.Count}, " +
            $"leaseRetained={rollout.ActiveWriterLease is not null}");
        await CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
            scope,
            scenario,
            result);
    }

    [Fact]
    public async Task WriterLease_ValidAtEntryButExpiredBeforePhysicalMongoUpdate_FailsWithZeroEffectsAndRetainsLease()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(-119));
        var updateStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowUpdate = new ManualResetEventSlim(false);
        var intercepted = 0;
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? throw new InvalidOperationException("MDM_TEST_MONGO_REQUIRED"));
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(command =>
        {
            if (command.CommandName == "update"
                && command.Command.TryGetValue("update", out var collection)
                && collection.IsString
                && collection.AsString == "mdm_product_legal_entity_scope_rollout_states"
                && Interlocked.CompareExchange(ref intercepted, 1, 0) == 0)
            {
                updateStarted.TrySetResult();
                allowUpdate.Wait(TimeSpan.FromSeconds(10));
            }
        });
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName),
            new ProductScopeMongoScope.TenantContext(scope.TenantId));

        var operation = guarded.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        try
        {
            await updateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var remaining = scenario.Lease.ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining.Add(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            allowUpdate.Set();
        }

        var result = await operation;
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.True(scenario.Lease.ExpiresAtUtc < DateTimeOffset.UtcNow);
        Assert.True(
            !result.Succeeded
            && result.VerifiedZeroMutation
            && result.LeaseRetentionRequired
            && persisted.Version == 0
            && persisted.ScopePeriods.Count == 1
            && persisted.AuditIntents.Count == 1
            && rollout.ActiveWriterLease?.Token == scenario.Lease.Token
            && rollout.ActiveWriterLease.Generation == scenario.Lease.Generation,
            $"mid-flight expiry outcome: succeeded={result.Succeeded}, " +
            $"zero={result.VerifiedZeroMutation}, version={persisted.Version}, " +
            $"periods={persisted.ScopePeriods.Count}, audits={persisted.AuditIntents.Count}, " +
            $"leaseRetained={rollout.ActiveWriterLease is not null}");
        await CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
            scope,
            scenario,
            result);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("actor")]
    [InlineData("tenant")]
    public async Task WriterLease_ExpiredIdentityDrift_IsRejectedWithoutMutationAndRetainsOriginalLease(
        string drift)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await SetLeaseWindowAsync(
            scope,
            scenario.Lease,
            DateTimeOffset.UtcNow.AddSeconds(
                -ProductLegalEntityScopeWriterLease.DurationSeconds - 1));
        var authority = scenario.Authority;
        var tenantId = scope.TenantId;
        if (drift == "payload")
        {
            var period = Assert.Single(
                scenario.Requested.ScopePeriods,
                item => item.CommandId == scenario.Lease.CommandId);
            period.Mode = ProductLegalEntityScopeMode.Scoped;
            period.LegalEntityIds = [Guid.NewGuid()];
        }
        else if (drift == "actor")
        {
            authority = await IssueAuthorityAsync(
                scope.TenantId,
                Guid.NewGuid(),
                scenario.Initial.Id,
                ProductLegalEntityScopeMutationIdentity.Create(scenario.Command));
        }
        else
        {
            tenantId = Guid.NewGuid();
            authority = await IssueAuthorityAsync(
                tenantId,
                scenario.Lease.ActorId,
                scenario.Initial.Id,
                ProductLegalEntityScopeMutationIdentity.Create(scenario.Command));
        }
        var guarded = new ProductLegalEntityScopeGuardedWriteSession(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(tenantId));

        var result = await guarded.ReplaceAsync(
            authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        Assert.Equal(0, persisted.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease!.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease.Generation);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task WriterLease_MissingOrMalformedExpiry_FailsClosedWithoutMutationAndCompletionRetainsLease(
        string expiryShape)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        var rawRollouts = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");
        var rolloutFilter = Builders<BsonDocument>.Filter.Eq(
            "_id",
            new BsonBinaryData(rollout.Id, GuidRepresentation.Standard));
        var expiryPath = "ActiveWriterLease.ExpiresAtUtc";
        var expiryUpdate = expiryShape == "missing"
            ? Builders<BsonDocument>.Update.Unset(expiryPath)
            : Builders<BsonDocument>.Update.Set(expiryPath, "invalid-expiry");
        var update = await rawRollouts.UpdateOneAsync(rolloutFilter, expiryUpdate);
        Assert.Equal(1, update.ModifiedCount);

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);
        await CompleteForegroundReplaceAsync(scope, scenario, result);

        var persistedPolicy = Assert.IsType<ProductLegalEntityScopePolicy>(
            await scope.PolicyRepository().GetByGlobalProductIdAsync(
                scenario.Initial.GlobalProductId));
        var persistedRollout = Assert.IsType<BsonDocument>(
            await rawRollouts.Find(rolloutFilter).FirstOrDefaultAsync());
        var persistedLease = persistedRollout["ActiveWriterLease"].AsBsonDocument;
        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        Assert.True(result.LeaseRetentionRequired);
        Assert.Equal(0, persistedPolicy.Version);
        Assert.Single(persistedPolicy.ScopePeriods);
        Assert.Single(persistedPolicy.AuditIntents);
        Assert.Equal(
            new BsonBinaryData(scenario.Lease.Token, GuidRepresentation.Standard),
            persistedLease["Token"]);
        Assert.Equal(scenario.Lease.Generation, persistedLease["Generation"].ToInt64());
        Assert.Equal(expiryShape != "missing", persistedLease.Contains("ExpiresAtUtc"));
    }

    [Fact]
    public async Task GuardedWrite_DirectRepositoryBypass_DeniesReplaceWithoutPolicyOrAuditMutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);

        var result = await scenario.Repository.UpdateAsync(scenario.Requested, 0);

        Assert.False(result.Succeeded);
        Assert.True(result.VersionConflict);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
    }

    [Fact]
    public async Task GuardedWrite_PreparationChangesAfterLease_RollsBackPolicyAndAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                item => item.Mode,
                ProductLegalEntityScopeRolloutMode.Enforced));

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.ScopePeriods);
        Assert.Single(persisted.AuditIntents);
        Assert.Equal(
            ProductLegalEntityScopeRolloutMode.Enforced,
            (await scope.RolloutRepository().GetAsync())!.Mode);
    }

    [Fact]
    public async Task GuardedWrite_PolicyCasFailureAfterRolloutGuard_RollsBackGuardPolicyAndAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        var rawRollout = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set(
                nameof(ProductLegalEntityScopePolicy.Version),
                7));
        var tenantFilter = Builders<BsonDocument>.Filter.Eq(
            nameof(ProductLegalEntityScopeRolloutState.TenantId),
            scope.TenantId);
        var policyFilter = Builders<BsonDocument>.Filter.Eq(
            "_id",
            scenario.Initial.Id);
        var rolloutBefore = await rawRollout.Find(tenantFilter).SingleAsync();
        var policyBefore = await rawPolicies.Find(policyFilter).SingleAsync();

        var result = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            scenario.Lease,
            scenario.Requested,
            0);

        Assert.False(result.Succeeded);
        Assert.True(result.VerifiedZeroMutation);
        var rolloutAfter = await rawRollout.Find(tenantFilter).SingleAsync();
        var policyAfter = await rawPolicies.Find(policyFilter).SingleAsync();
        Assert.True(rolloutBefore.Equals(rolloutAfter));
        Assert.True(policyBefore.Equals(policyAfter));
    }

    [Fact]
    public async Task GuardedWrite_WrongTokenGenerationOrMutation_RollsBackAllEffects()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var staleLease = CopyLease(scenario.Lease);
        staleLease.Generation++;

        var stale = await scenario.Repository.ReplaceAsync(
            scenario.Authority,
            staleLease,
            scenario.Requested,
            0);
        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);

        Assert.False(stale.Succeeded);
        Assert.True(stale.VerifiedZeroMutation);
        Assert.Equal(0, persisted!.Version);
        Assert.Single(persisted.AuditIntents);
        Assert.NotNull((await scope.RolloutRepository().GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task GuardedWrite_TwoContenders_CommitExactlyOneMutationAndOneAudit()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);

        var results = await Task.WhenAll(
            scenario.Repository.ReplaceAsync(
                scenario.Authority, scenario.Lease, scenario.Requested, 0),
            scenario.Repository.ReplaceAsync(
                scenario.Authority, scenario.Lease, scenario.Requested, 0));

        var persisted = await scope.PolicyRepository().GetByGlobalProductIdAsync(
            scenario.Initial.GlobalProductId);
        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => !result.Succeeded));
        Assert.Equal(1, persisted!.Version);
        Assert.Equal(2, persisted.ScopePeriods.Count);
        Assert.Equal(2, persisted.AuditIntents.Count);
    }

    [Fact]
    public async Task GuardedWrite_CompleteDocumentOverOneMiB_FailsBeforeMutationAndPreservesUnknownBson()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var scenario = await CreateGuardedReplaceScenarioAsync(scope);
        var rawPolicies = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_policies");
        await rawPolicies.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id),
            Builders<BsonDocument>.Update.Set(
                "FutureOversizedField",
                new string('X', ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scenario.Repository.ReplaceAsync(
                scenario.Authority,
                scenario.Lease,
                scenario.Requested,
                0));

        var persisted = await rawPolicies.Find(
            Builders<BsonDocument>.Filter.Eq("_id", scenario.Initial.Id)).SingleAsync();
        Assert.Equal(0, persisted[nameof(ProductLegalEntityScopePolicy.Version)].AsInt32);
        Assert.Equal(
            ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes,
            persisted["FutureOversizedField"].AsString.Length);
        Assert.Single(persisted[nameof(ProductLegalEntityScopePolicy.AuditIntents)].AsBsonArray);
    }

    [Fact]
    public async Task Same_command_replays_exact_lease_and_stale_generation_cannot_release_no_ABA()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        var requested = Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "A");

        var first = await repository.AcquireWriterLeaseAsync(requested);
        var replay = await repository.AcquireWriterLeaseAsync(Lease(requested.CommandId, requested.ActorId, requested.MutationKind, "A"));
        Assert.True(first.Acquired);
        Assert.Equal(first.Lease!.Token, replay.Lease!.Token);
        Assert.Equal(first.Lease.Generation, replay.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
        Assert.True(await repository.BindWriterLeaseBaselineAsync(first.Lease.Token, first.Lease.Generation, new string('B', 64)));
        Assert.False(await repository.ReleaseWriterLeaseAsync(Guid.NewGuid(), first.Lease.Generation));
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation + 1));
        Assert.True(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));

        var second = await repository.AcquireWriterLeaseAsync(Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "C"));
        Assert.True(second.Acquired);
        Assert.True(second.Lease!.Generation > first.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
    }

    [Fact]
    public async Task Pre_H1b_rollout_without_writer_fields_acquires_generation_one_without_migration()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == state.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update
                .Unset(nameof(ProductLegalEntityScopeRolloutState.WriterLeaseGeneration))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveWriterLease))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveFence)));

        var acquired = await repository.AcquireWriterLeaseAsync(
            Lease(Guid.NewGuid(), Guid.NewGuid(), "RequestProductAbbreviationAllocation", "L"));

        Assert.True(acquired.Acquired);
        Assert.NotNull(acquired.Lease);
        Assert.Equal(1, acquired.Lease.Generation);
        var persisted = await scope.RolloutCollection
            .Find(item => item.TenantId == scope.TenantId && item.Id == state.Id)
            .SingleAsync();
        Assert.Equal(1, persisted.WriterLeaseGeneration);
        Assert.Equal(acquired.Lease.Token, persisted.ActiveWriterLease!.Token);
    }

    [Fact]
    public async Task Tenant_B_cannot_observe_or_release_tenant_A_lease_and_payload_drift_is_denied()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenantA = scope.RolloutRepository();
        Assert.True((await tenantA.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var command = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var acquired = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "A"));
        var drift = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "B"));
        var tenantB = scope.RolloutRepository(Guid.NewGuid());

        Assert.False(drift.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", drift.FailureCode);
        Assert.Null(await tenantB.GetAsync());
        Assert.False(await tenantB.ReleaseWriterLeaseAsync(acquired.Lease!.Token, acquired.Lease.Generation));
    }

    [Fact]
    public async Task Coordinator_zero_delta_releases_while_202_exception_and_cancellation_retain()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());

        var first = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await coordinator.CompleteAsync(first, false, 409, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        foreach (var (status, exceptional) in new[] { (202, false), (500, true), (499, true) })
        {
            var admission = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            await coordinator.CompleteAsync(admission, false, status, exceptional, CancellationToken.None);
            Assert.NotNull((await repository.GetAsync())!.ActiveWriterLease);
            var lease = (await repository.GetAsync())!.ActiveWriterLease!;
            Assert.True(await repository.ReleaseWriterLeaseAsync(lease.Token, lease.Generation));
        }
    }

    [Fact]
    public async Task Coordinator_ignores_delivery_owned_progress_but_retains_for_partial_immutable_intent()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(rollout)).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var collection = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");

        var deliveryOnly = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntentReceipts", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "CentralAcknowledgement", "receipt-progress" }
            }));
        await coordinator.CompleteAsync(deliveryOnly, false, 409, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        var partialMutation = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntents", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard) },
                { "AggregateType", (int)AuditAggregateType.ProductLegalEntityScopeRolloutState },
                { "AggregateId", new BsonBinaryData(rollout.Id, GuidRepresentation.Standard) },
                { "SourceService", AuditIntentContract.SourceService },
                { "CommandId", Guid.NewGuid().ToString("D") }, { "EvidenceHash", new string('A', 64) }
            }));
        await coordinator.CompleteAsync(partialMutation, false, 409, false, CancellationToken.None);
        Assert.NotNull((await repository.GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task Coordinator_allows_only_exact_GSKU_facade_inner_nesting()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var productId = Guid.NewGuid();
        const string operation = "exact-operation";
        var facade = new CreateFirstGskuDraftFacadeCommand(
            new ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest
            {
                GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA"
            }, operation);
        var inner = new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA",
            CreationCommandId = $"GSKU:{operation.ToUpperInvariant()}"
        });

        var parent = await coordinator.EnterAsync(facade, CancellationToken.None);
        Assert.True(parent.OwnsLease);
        using (coordinator.Activate(parent))
        {
            var exactChild = await coordinator.EnterAsync(inner, CancellationToken.None);
            var unrelated = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            Assert.True(exactChild.IsAllowed); Assert.False(exactChild.OwnsLease);
            Assert.False(unrelated.IsAllowed);
            Assert.Equal("PRODUCT_SCOPE_NESTED_MUTATION_NOT_AUTHORIZED", unrelated.FailureCode);
            await coordinator.CompleteAsync(exactChild, true, 201, false, CancellationToken.None);
        }
        await coordinator.CompleteAsync(parent, true, 201, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        var directInner = await coordinator.EnterAsync(inner, CancellationToken.None);
        Assert.True(directInner.OwnsLease);
        await coordinator.CompleteAsync(directInner, true, 201, false, CancellationToken.None);
    }

    private static async Task<GuardedReplaceScenario> CreateGuardedReplaceScenarioAsync(
        ProductScopeMongoScope scope)
    {
        var repository = scope.PolicyRepository();
        var productId = Guid.NewGuid();
        var initial = ProductLegalEntityScopeMongoTests.CreatePolicy(
            scope.TenantId,
            productId,
            Guid.NewGuid());
        Assert.True((await repository.CreateAsync(initial)).Succeeded);
        var rolloutRepository = scope.RolloutRepository();
        Assert.True((await rolloutRepository.CreateAsync(
            ProductLegalEntityScopeRolloutState.CreatePreparation(
                scope.TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now))).Succeeded);

        var subjectId = Guid.NewGuid();
        var command = new ReplaceProductLegalEntityScopePolicyCommand(
            productId,
            Guid.NewGuid(),
            new()
            {
                ExpectedVersion = 0,
                Mode = ProductLegalEntityScopeMode.GroupWide
            });
        var mutation = ProductLegalEntityScopeMutationIdentity.Create(command);
        var authority = await IssueAuthorityAsync(
            scope.TenantId,
            subjectId,
            initial.Id,
            mutation);
        var leaseAcquiredAtUtc = DateTimeOffset.UtcNow;
        var leaseRequest = new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(),
            Generation = 1,
            CommandId = mutation.CommandId,
            ActorId = subjectId,
            MutationKind = mutation.Kind,
            PayloadFingerprint = mutation.PayloadFingerprint,
            Owner = "Diten.MDM:ForegroundProductLegalEntityScopeReplace",
            AcquiredAtUtc = leaseAcquiredAtUtc,
            ExpiresAtUtc = leaseAcquiredAtUtc.AddSeconds(
                ProductLegalEntityScopeWriterLease.DurationSeconds)
        };
        var acquired = await rolloutRepository.AcquireWriterLeaseAsync(leaseRequest);
        Assert.True(acquired.Acquired);
        Assert.True(await rolloutRepository.BindWriterLeaseBaselineAsync(
            acquired.Lease!.Token,
            acquired.Lease.Generation,
            new string('B', 64)));
        var replay = await rolloutRepository.AcquireWriterLeaseAsync(leaseRequest);
        var lease = Assert.IsType<ProductLegalEntityScopeWriterLease>(replay.Lease);
        Assert.True(lease.BaselineBound);

        var requested = Assert.IsType<ProductLegalEntityScopePolicy>(
            await repository.GetByGlobalProductIdAsync(productId));
        var changedAt = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        requested.ReplaceCurrent(
            0,
            command.CommandId,
            ProductLegalEntityScopeMode.GroupWide,
            [],
            subjectId,
            changedAt);
        ProductLegalEntityScopeMongoTests.AddPolicyUpdateAuditIntent(
            requested,
            ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
            0);
        return new GuardedReplaceScenario(
            repository,
            initial,
            requested,
            authority,
            lease,
            command);
    }

    private static async Task SetLeaseWindowAsync(
        ProductScopeMongoScope scope,
        ProductLegalEntityScopeWriterLease lease,
        DateTimeOffset acquiredAtUtc)
    {
        var acquiredTicks = acquiredAtUtc.UtcDateTime.Ticks;
        acquiredAtUtc = new DateTimeOffset(
            acquiredTicks - acquiredTicks % TimeSpan.TicksPerMillisecond,
            TimeSpan.Zero);
        lease.AcquiredAtUtc = acquiredAtUtc;
        lease.ExpiresAtUtc = acquiredAtUtc.AddSeconds(
            ProductLegalEntityScopeWriterLease.DurationSeconds);
        var update = await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                item => item.ActiveWriterLease,
                lease));
        Assert.Equal(1, update.ModifiedCount);
    }

    private static async Task CompleteForegroundReplaceAndAssertLeaseRetainedAsync(
        ProductScopeMongoScope scope,
        GuardedReplaceScenario scenario,
        ProductLegalEntityScopePolicyWriteResult result)
    {
        await CompleteForegroundReplaceAsync(scope, scenario, result);

        var rollout = Assert.IsType<ProductLegalEntityScopeRolloutState>(
            await scope.RolloutRepository().GetAsync());
        Assert.Equal(scenario.Lease.Token, rollout.ActiveWriterLease?.Token);
        Assert.Equal(scenario.Lease.Generation, rollout.ActiveWriterLease?.Generation);
    }

    private static async Task CompleteForegroundReplaceAsync(
        ProductScopeMongoScope scope,
        GuardedReplaceScenario scenario,
        ProductLegalEntityScopePolicyWriteResult result)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(
            scope.RolloutRepository(),
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(scenario.Lease.ActorId.ToString("D")),
            tenant,
            TimeProvider.System);
        var admission = new ProductLegalEntityScopeWriteAdmission(
            true,
            true,
            false,
            null,
            new ProductLegalEntityScopeWriteFenceCoordinator.LeaseContext(
                scope.TenantId,
                scenario.Lease,
                scenario.Command));

        await coordinator.CompleteForegroundReplaceAsync(
            admission,
            result,
            CancellationToken.None);
    }

    private static async Task<ProductLegalEntityScopeVerifiedWriterAuthority> IssueAuthorityAsync(
        Guid tenantId,
        Guid subjectId,
        Guid aggregateId,
        ProductLegalEntityScopeMutationIdentity mutation)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("actor_type", "tenant_user"),
                new Claim("tenant_id", tenantId.ToString("D")),
                new Claim("sub", subjectId.ToString("D")),
                new Claim("permission", "mdm.product-legal-entity-scopes.replace")
            ], "test"))
        };
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString("D");
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var provider = new ProductLegalEntityScopeWriterAuthorityProvider(
            new HttpContextAccessor { HttpContext = context },
            tenant);
        return Assert.IsType<ProductLegalEntityScopeVerifiedWriterAuthority>(
            await provider.ResolveForegroundReplaceAsync(aggregateId, mutation));
    }

    private static ProductLegalEntityScopeWriterLease CopyLease(
        ProductLegalEntityScopeWriterLease source) => new()
        {
            Token = source.Token,
            Generation = source.Generation,
            CommandId = source.CommandId,
            ActorId = source.ActorId,
            MutationKind = source.MutationKind,
            PayloadFingerprint = source.PayloadFingerprint,
            Owner = source.Owner,
            AcquiredAtUtc = source.AcquiredAtUtc,
            ExpiresAtUtc = source.ExpiresAtUtc,
            PreWriteStateHash = source.PreWriteStateHash
        };

    private sealed record GuardedReplaceScenario(
        ProductLegalEntityScopePolicyRepository Repository,
        ProductLegalEntityScopePolicy Initial,
        ProductLegalEntityScopePolicy Requested,
        ProductLegalEntityScopeVerifiedWriterAuthority Authority,
        ProductLegalEntityScopeWriterLease Lease,
        ReplaceProductLegalEntityScopePolicyCommand Command);

    private static CreateLskuDraftCommand Command(Guid identity) => new(new ProductItemSkuMasterModels.CreateLskuDraftRequest
    {
        GskuId = Guid.NewGuid(), MarketCode = "TR", IdempotencyKey = identity.ToString("D")
    });
    private sealed class Actor(string actorId) : IProductIdentityActorContext { public string ActorId => actorId; }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }


    private static ProductLegalEntityScopeWriterLease Lease(
        Guid command,
        Guid actor,
        string kind,
        string fingerprintSeed)
    {
        var acquiredAtUtc = DateTimeOffset.UtcNow;
        return new()
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = command, ActorId = actor,
            MutationKind = kind, PayloadFingerprint = new string(fingerprintSeed[0], 64),
            Owner = $"Diten.MDM:{kind}", AcquiredAtUtc = acquiredAtUtc,
            ExpiresAtUtc = acquiredAtUtc.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds)
        };
    }
}
