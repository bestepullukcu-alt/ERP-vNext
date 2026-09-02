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
public sealed class ProductIdentityWorkflowOperationRecoveryMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoClient _client = null!;
    private IMongoDatabase _database = null!;

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
        _client = new MongoClient(settings);
        _database = _client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        EnsureOperationIndexes(_tenantId);
    }

    [Fact]
    public async Task Four_families_abandon_atomically_append_audit_and_are_not_rediscovered()
    {
        foreach (var fixture in await CreateFourFamiliesAsync())
        {
            var repository = Repository(_tenantId);
            var candidate = await repository.GetCandidateAsync(fixture.OperationId);
            Assert.NotNull(candidate);
            Assert.Equal(fixture.Scope.Family, candidate!.Scope.Family);

            var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents);
            var result = await repository.RecoverAsync(mutation);
            var replay = await repository.RecoverAsync(mutation);

            Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied, result.Status);
            Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay, replay.Status);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart, result.Disposition);
            Assert.Equal(fixture.Scope.Family == ProductIdentityWorkflowOperationFamily.FirstGsku ? 2 : 1,
                await AuditCountAsync(fixture));
            Assert.Equal(ProductIdentityLifecycleStatus.Draft, await LifecycleAsync(fixture));
            Assert.Equal(1, await TargetVersionAsync(fixture));
            Assert.DoesNotContain(fixture.OperationId, await DiscoverAsync(fixture.Scope.Family));
        }
    }

    [Fact]
    public async Task Supersede_has_one_concurrent_winner_one_successor_and_payload_drift_conflicts()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var successorScope = new GlobalProductWorkflowRecoveryScope(
            ((GlobalProductWorkflowRecoveryScope)fixture.Scope).GlobalProductId, 1);
        var successor = new ProductIdentityWorkflowOperationRecoverySuccessor(
            Guid.NewGuid(), $"recovery:{Guid.NewGuid():D}", Hex('b'), Guid.NewGuid(), successorScope);
        var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents,
            ProductIdentityWorkflowRecoveryDisposition.Superseded, successor);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Repository(_tenantId).RecoverAsync(mutation)));

        Assert.Single(results, item => item.Status == ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied);
        Assert.Equal(7, results.Count(item => item.Status is ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay
            or ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict));
        var operationCollection = _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);
        var operations = await operationCollection.Find(item => item.TenantId == _tenantId).ToListAsync();
        Assert.Equal(2, operations.Count);
        Assert.Single(operations, item => item.OperationId == successor.OperationId
            && item.MakerSubjectId == mutation.Successor!.MakerSubjectId
            && item.ExpectedProductVersion == 1
            && item.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Prepared);

        var driftEvidence = mutation.Evidence with { ReasonCode = "DRIFT" };
        var drift = new ProductIdentityWorkflowOperationRecoveryMutation(mutation.OperationId,
            mutation.ExpectedOperationVersion, mutation.ExpectedScope, mutation.ExpectedOriginalMakerSubjectId,
            mutation.ExpectedStartIdempotencyKey, mutation.ExpectedOperationFingerprint, mutation.ExpectedLeaseOwner,
            mutation.ExpectedLeaseUntilUtcTicksV1, mutation.ExpectedLeaseGeneration, driftEvidence,
            mutation.Successor, mutation.AuditIntents);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(drift)).Status);
    }

    [Fact]
    public async Task Stale_versions_fingerprint_active_lease_and_cross_tenant_fail_closed_without_audit()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.Lsku);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;

        var staleScope = new LskuWorkflowRecoveryScope(
            ((LskuWorkflowRecoveryScope)fixture.Scope).LskuId,
            ((LskuWorkflowRecoveryScope)fixture.Scope).GskuId,
            ((LskuWorkflowRecoveryScope)fixture.Scope).ProductDefinitionRevisionId,
            ((LskuWorkflowRecoveryScope)fixture.Scope).MarketCode, 9);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(Mutation(candidate, staleScope, fixture.AuditIntents))).Status);

        var staleFingerprint = new ProductIdentityWorkflowOperationRecoveryMutation(candidate.OperationId,
            candidate.OperationVersion, fixture.Scope, candidate.OriginalMakerSubjectId,
            candidate.StartIdempotencyKey, Hex('f'), candidate.LeaseOwner, candidate.LeaseUntilUtcTicksV1,
            candidate.LeaseGeneration, Evidence(), null, fixture.AuditIntents);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(staleFingerprint)).Status);

        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _database.GetCollection<LskuIdentityWorkflowOperation>(LskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == fixture.OperationId,
                Builders<LskuIdentityWorkflowOperation>.Update.Set(item => item.LeaseOwner, "active")
                    .Set(item => item.LeaseUntilUtcTicksV1, now + TimeSpan.FromMinutes(5).Ticks)
                    .Inc(item => item.LeaseGeneration, 1).Inc(item => item.Version, 1));
        var leased = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible,
            (await Repository(_tenantId).RecoverAsync(Mutation(leased, fixture.Scope, fixture.AuditIntents))).Status);
        Assert.Null(await Repository(_otherTenantId).GetCandidateAsync(fixture.OperationId));
        Assert.Equal(0, await AuditCountAsync(fixture));
        Assert.Equal(0, await TargetVersionAsync(fixture));
    }

    [Fact]
    public async Task FirstGsku_pair_is_all_or_nothing_when_second_target_CAS_is_stale()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.FirstGsku);
        var scope = (FirstGskuWorkflowRecoveryScope)fixture.Scope;
        await _database.GetCollection<Gsku>("mdm_gskus")
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.Id == scope.GskuId,
                Builders<Gsku>.Update.Inc(item => item.Version, 1));
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;

        var result = await Repository(_tenantId).RecoverAsync(Mutation(candidate, fixture.Scope, fixture.AuditIntents));

        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, result.Status);
        var revision = await _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(item => item.TenantId == _tenantId && item.Id == scope.ProductDefinitionRevisionId).SingleAsync();
        var operation = await _database.GetCollection<FirstGskuIdentityWorkflowOperation>(
                FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(item => item.TenantId == _tenantId && item.OperationId == fixture.OperationId).SingleAsync();
        Assert.Equal(0, revision.Version);
        Assert.Empty(revision.AuditIntents);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Prepared, operation.Checkpoint);
        Assert.Null(operation.RecoveryCommandId);
    }

    [Fact]
    public async Task Expired_lease_is_recoverable_but_positive_workflow_evidence_is_ineligible()
    {
        var fixtures = await CreateFourFamiliesAsync();
        var expired = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.FinishedGood);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
                FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == expired.OperationId,
                Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.LeaseOwner, "stale")
                    .Set(item => item.LeaseUntilUtcTicksV1, now - 1)
                    .Inc(item => item.LeaseGeneration, 1).Inc(item => item.Version, 1));
        var expiredCandidate = (await Repository(_tenantId).GetCandidateAsync(expired.OperationId))!;
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied,
            (await Repository(_tenantId).RecoverAsync(Mutation(expiredCandidate, expired.Scope,
                expired.AuditIntents))).Status);

        var positive = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == positive.OperationId,
                Builders<GlobalProductIdentityWorkflowOperation>.Update.Set(item => item.WorkflowInstanceId,
                    Guid.NewGuid()));
        var positiveCandidate = (await Repository(_tenantId).GetCandidateAsync(positive.OperationId))!;
        Assert.True(positiveCandidate.HasPersistedWorkflowEvidence);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible,
            (await Repository(_tenantId).RecoverAsync(Mutation(positiveCandidate, positive.Scope,
                positive.AuditIntents))).Status);
        Assert.Equal(0, await AuditCountAsync(positive));
    }

    [Fact]
    public async Task Replay_fails_closed_when_persisted_audit_proof_drifts()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied,
            (await Repository(_tenantId).RecoverAsync(mutation)).Status);

        var scope = (GlobalProductWorkflowRecoveryScope)fixture.Scope;
        await _database.GetCollection<BsonDocument>("mdm_global_products").UpdateOneAsync(
            new BsonDocument { { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
                { "_id", new BsonBinaryData(scope.GlobalProductId, GuidRepresentation.Standard) } },
            new BsonDocument("$set", new BsonDocument("AuditIntents.0.EvidenceHash", new string('B', 64))));

        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(mutation)).Status);
    }

    [Fact]
    public async Task Audit_budget_and_precancelled_request_do_not_mutate_operation_or_target()
    {
        var fixtures = await CreateFourFamiliesAsync();
        var budget = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var scope = (GlobalProductWorkflowRecoveryScope)budget.Scope;
        var existing = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate)
            .Select(_ => Audit(scope.GlobalProductId, AuditAggregateType.GlobalProduct)).ToArray();
        await _database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            item => item.TenantId == _tenantId && item.Id == scope.GlobalProductId,
            Builders<GlobalProduct>.Update.PushEach(item => item.AuditIntents, existing));
        var candidate = (await Repository(_tenantId).GetCandidateAsync(budget.OperationId))!;
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(Mutation(candidate, budget.Scope, budget.AuditIntents))).Status);
        Assert.Equal(0, await TargetVersionAsync(budget));

        var cancelled = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.Lsku);
        var cancelledCandidate = (await Repository(_tenantId).GetCandidateAsync(cancelled.OperationId))!;
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository(_tenantId).RecoverAsync(
            Mutation(cancelledCandidate, cancelled.Scope, cancelled.AuditIntents), source.Token));
        Assert.Equal(0, await TargetVersionAsync(cancelled));
        Assert.Equal(0, await AuditCountAsync(cancelled));
    }

    [Fact]
    public async Task Supersede_and_exact_replay_use_one_persisted_successor_in_each_family()
    {
        foreach (var fixture in await CreateFourFamiliesAsync())
        {
            var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
            var successor = new ProductIdentityWorkflowOperationRecoverySuccessor(Guid.NewGuid(),
                $"recovery:{Guid.NewGuid():D}", Hex('b'), Guid.NewGuid(), NextScope(fixture.Scope));
            var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents,
                ProductIdentityWorkflowRecoveryDisposition.Superseded, successor);
            Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied,
                (await Repository(_tenantId).RecoverAsync(mutation)).Status);
            var replay = await Repository(_tenantId).RecoverAsync(mutation);
            Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay, replay.Status);
            Assert.Equal(successor.OperationId, replay.Successor?.OperationId);
            Assert.Equal(successor.Scope, replay.Successor?.Scope);
            Assert.Equal(1, await TargetVersionAsync(fixture));
            Assert.Equal(ProductIdentityLifecycleStatus.Draft, await LifecycleAsync(fixture));
        }
    }

    [Fact]
    public async Task Malformed_lease_and_soft_deleted_operation_are_never_recovered()
    {
        var fixtures = await CreateFourFamiliesAsync();
        var malformed = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.Lsku);
        await _database.GetCollection<LskuIdentityWorkflowOperation>(
                LskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == malformed.OperationId,
                Builders<LskuIdentityWorkflowOperation>.Update.Set(item => item.LeaseOwner, "orphaned-owner"));
        var candidate = (await Repository(_tenantId).GetCandidateAsync(malformed.OperationId))!;
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible,
            (await Repository(_tenantId).RecoverAsync(Mutation(candidate, malformed.Scope,
                malformed.AuditIntents))).Status);
        Assert.Equal(0, await AuditCountAsync(malformed));

        var deleted = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.FinishedGood);
        await _database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
                FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == deleted.OperationId,
                Builders<FinishedGoodIdentityWorkflowOperation>.Update.Set(item => item.IsDeleted, true)
                    .Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        Assert.Null(await Repository(_tenantId).GetCandidateAsync(deleted.OperationId));
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.NotFound,
            (await Repository(_tenantId).RecoverAsync(Mutation(
                new ProductIdentityWorkflowOperationRecoveryCandidate(deleted.OperationId, 0, deleted.Scope,
                    Guid.NewGuid(), "deleted", Hex('a'), true, false,
                    ProductIdentityWorkflowRecoveryDisposition.None, null, null, 0),
                deleted.Scope, deleted.AuditIntents))).Status);
        Assert.DoesNotContain(deleted.OperationId, await DiscoverAsync(deleted.Scope.Family));
    }

    [Fact]
    public async Task Exact_replay_survives_audit_delivery_target_progress_and_successor_workflow_start()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var successor = new ProductIdentityWorkflowOperationRecoverySuccessor(Guid.NewGuid(),
            $"recovery:{Guid.NewGuid():D}", Hex('b'), Guid.NewGuid(), NextScope(fixture.Scope));
        var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents,
            ProductIdentityWorkflowRecoveryDisposition.Superseded, successor);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied,
            (await Repository(_tenantId).RecoverAsync(mutation)).Status);

        var scope = (GlobalProductWorkflowRecoveryScope)fixture.Scope;
        await _database.GetCollection<BsonDocument>("mdm_global_products").UpdateOneAsync(
            new BsonDocument { { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
                { "_id", new BsonBinaryData(scope.GlobalProductId, GuidRepresentation.Standard) } },
            new BsonDocument { { "$set", new BsonDocument
                {
                    { "AuditIntents.0.DeliveryState", 2 }, { "AuditIntents.0.AttemptCount", 1 },
                    { "AuditIntents.0.CentralAcknowledgement", "ack" },
                    { "AuditIntents.0.CentralIdempotencyKey", "central-key" }
                } }, { "$inc", new BsonDocument("Version", 1) } });
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == successor.OperationId,
                Builders<GlobalProductIdentityWorkflowOperation>.Update
                    .Set(item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted)
                    .Set(item => item.WorkflowInstanceId, Guid.NewGuid()).Inc(item => item.Version, 1));

        var replay = await Repository(_tenantId).RecoverAsync(mutation);
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay, replay.Status);
        Assert.Equal(successor.OperationId, replay.Successor?.OperationId);
        Assert.Equal(successor.Scope, replay.Successor?.Scope);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("command")]
    [InlineData("causation")]
    public async Task Audit_actor_and_command_binding_mismatch_rolls_back(string field)
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.FinishedGood);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var mutation = Mutation(candidate, fixture.Scope, fixture.AuditIntents);
        if (field == "actor") mutation.AuditIntents[0].ActorId = Guid.NewGuid().ToString("D");
        if (field == "command") mutation.AuditIntents[0].CommandId = Guid.NewGuid().ToString("D");
        if (field == "causation") mutation.AuditIntents[0].CausationId = Guid.NewGuid().ToString("D");

        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(mutation)).Status);
        Assert.Equal(0, await TargetVersionAsync(fixture));
        Assert.Equal(0, await AuditCountAsync(fixture));
    }

    [Fact]
    public async Task Operation_CAS_loss_and_target_CAS_loss_leave_no_partial_recovery()
    {
        var fixtures = await CreateFourFamiliesAsync();
        var operationLoss = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.Lsku);
        var operationCandidate = (await Repository(_tenantId).GetCandidateAsync(operationLoss.OperationId))!;
        await _database.GetCollection<LskuIdentityWorkflowOperation>(LskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantId && item.OperationId == operationLoss.OperationId,
                Builders<LskuIdentityWorkflowOperation>.Update.Inc(item => item.Version, 1));
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(Mutation(operationCandidate, operationLoss.Scope,
                operationLoss.AuditIntents))).Status);
        Assert.Equal(0, await TargetVersionAsync(operationLoss));
        Assert.Equal(0, await AuditCountAsync(operationLoss));

        var targetLoss = Assert.Single(fixtures,
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var targetCandidate = (await Repository(_tenantId).GetCandidateAsync(targetLoss.OperationId))!;
        var targetScope = (GlobalProductWorkflowRecoveryScope)targetLoss.Scope;
        await _database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            item => item.TenantId == _tenantId && item.Id == targetScope.GlobalProductId,
            Builders<GlobalProduct>.Update.Inc(item => item.Version, 1));
        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
            (await Repository(_tenantId).RecoverAsync(Mutation(targetCandidate, targetLoss.Scope,
                targetLoss.AuditIntents))).Status);
        var oldOperation = await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(item => item.TenantId == _tenantId && item.OperationId == targetLoss.OperationId).SingleAsync();
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Prepared, oldOperation.Checkpoint);
        Assert.Null(oldOperation.RecoveryCommandId);
        Assert.Equal(0, await AuditCountAsync(targetLoss));
    }

    [Fact]
    public async Task Conflicting_successor_insert_rolls_back_old_operation_target_and_audit()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var successor = new ProductIdentityWorkflowOperationRecoverySuccessor(Guid.NewGuid(),
            $"recovery:{Guid.NewGuid():D}", Hex('b'), Guid.NewGuid(), NextScope(fixture.Scope));
        var collision = BaseGlobal(Guid.NewGuid(), Guid.NewGuid());
        collision.StartIdempotencyKey = successor.StartIdempotencyKey;
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(collision);

        var result = await Repository(_tenantId).RecoverAsync(Mutation(candidate, fixture.Scope,
            fixture.AuditIntents, ProductIdentityWorkflowRecoveryDisposition.Superseded, successor));

        Assert.Equal(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, result.Status);
        Assert.Equal(0, await TargetVersionAsync(fixture));
        Assert.Equal(0, await AuditCountAsync(fixture));
        var oldOperation = await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(item => item.TenantId == _tenantId && item.OperationId == fixture.OperationId).SingleAsync();
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Prepared, oldOperation.Checkpoint);
        Assert.Null(oldOperation.RecoveryCommandId);
    }

    [Fact]
    public async Task Real_TryClaim_race_with_recovery_has_exactly_one_winner_and_no_partial_loser()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var candidate = (await Repository(_tenantId).GetCandidateAsync(fixture.OperationId))!;
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var operationRepository = new GlobalProductIdentityWorkflowOperationRepository(
            _database, new Tenant(_tenantId));
        var claimTask = operationRepository.TryClaimAsync(new GlobalProductIdentityWorkflowClaimRequest(
            fixture.OperationId, candidate.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.Prepared], "race-worker", now,
            now + TimeSpan.FromMinutes(1).Ticks));
        var recoveryTask = Repository(_tenantId).RecoverAsync(Mutation(candidate, fixture.Scope,
            fixture.AuditIntents));
        var claim = await claimTask;
        var recovery = await recoveryTask;

        var claimWon = claim is not null;
        var recoveryWon = recovery.Status == ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied;
        Assert.True(claimWon ^ recoveryWon);
        if (recoveryWon)
        {
            Assert.Equal(1, await TargetVersionAsync(fixture));
            Assert.Equal(1, await AuditCountAsync(fixture));
        }
        else
        {
            Assert.Contains(recovery.Status,
                new[] { ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict,
                    ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible });
            Assert.Equal(0, await TargetVersionAsync(fixture));
            Assert.Equal(0, await AuditCountAsync(fixture));
        }
    }

    [Fact]
    public async Task Coordinated_transaction_abort_after_successor_insert_proves_commit_failure_rollback_without_test_hook()
    {
        var fixture = Assert.Single(await CreateFourFamiliesAsync(),
            item => item.Scope.Family == ProductIdentityWorkflowOperationFamily.GlobalProduct);
        var scope = (GlobalProductWorkflowRecoveryScope)fixture.Scope;
        var successor = BaseGlobal(scope.GlobalProductId, Guid.NewGuid());
        successor.ExpectedProductVersion = 1;
        using var session = await _client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                    GlobalProductIdentityWorkflowOperationRepository.CollectionName)
                .UpdateOneAsync(session,
                    item => item.TenantId == _tenantId && item.OperationId == fixture.OperationId,
                    Builders<GlobalProductIdentityWorkflowOperation>.Update
                        .Set(item => item.Checkpoint,
                            GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart)
                        .Set(item => item.RecoveryCommandId, Guid.NewGuid()).Inc(item => item.Version, 1));
            await _database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(session,
                item => item.TenantId == _tenantId && item.Id == scope.GlobalProductId,
                Builders<GlobalProduct>.Update.Inc(item => item.Version, 1));
            await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                    GlobalProductIdentityWorkflowOperationRepository.CollectionName)
                .InsertOneAsync(session, successor);

            // A coordinated abort exercises the same server transaction guarantee without a production test hook
            // or a process-global configureFailPoint that could interfere with parallel Mongo tests.
            await session.AbortTransactionAsync();
        }
        finally
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync();
        }

        Assert.Equal(0, await TargetVersionAsync(fixture));
        var operations = await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(item => item.TenantId == _tenantId).ToListAsync();
        Assert.DoesNotContain(operations, item => item.OperationId == successor.OperationId);
        var oldOperation = Assert.Single(operations, item => item.OperationId == fixture.OperationId);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Prepared, oldOperation.Checkpoint);
        Assert.Null(oldOperation.RecoveryCommandId);
    }

    public async Task DisposeAsync()
    {
        foreach (var collectionName in Collections)
            await _database.GetCollection<BsonDocument>(collectionName).DeleteManyAsync(
                new BsonDocument("TenantId", new BsonDocument("$in", new BsonArray
                {
                    new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
                    new BsonBinaryData(_otherTenantId, GuidRepresentation.Standard)
                })));
    }

    private ProductIdentityWorkflowOperationRecoveryRepository Repository(Guid tenantId) =>
        new(_client, _database, new Tenant(tenantId));

    private static ProductIdentityWorkflowOperationRecoveryScope NextScope(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => new GlobalProductWorkflowRecoveryScope(
            item.GlobalProductId, item.GlobalProductVersion + 1),
        FirstGskuWorkflowRecoveryScope item => new FirstGskuWorkflowRecoveryScope(
            item.GlobalProductId, item.ProductDefinitionRevisionId,
            item.GskuId, item.GskuVersion + 1, item.ProductDefinitionRevisionVersion + 1),
        LskuWorkflowRecoveryScope item => new LskuWorkflowRecoveryScope(
            item.LskuId, item.GskuId, item.ProductDefinitionRevisionId,
            item.MarketCode, item.LskuVersion + 1),
        FinishedGoodWorkflowRecoveryScope item => new FinishedGoodWorkflowRecoveryScope(
            item.FinishedGoodId, item.GskuId,
            item.ProductDefinitionRevisionId, item.FinishedGoodVersion + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private async Task<IReadOnlyList<Fixture>> CreateFourFamiliesAsync()
    {
        var globalProduct = new GlobalProduct { Id = Guid.NewGuid(), TenantId = _tenantId,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, CanonicalCode = $"GP-{Guid.NewGuid():N}" };
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = _tenantId,
            GlobalProductId = globalProduct.Id, LifecycleStatus = ProductIdentityLifecycleStatus.Draft };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = _tenantId,
            ProductDefinitionRevisionId = revision.Id, LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            CanonicalCode = $"GS-{Guid.NewGuid():N}" };
        var lsku = new Lsku { Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = gsku.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, MarketCode = "TR",
            CanonicalCode = $"LS-{Guid.NewGuid():N}" };
        var finishedGood = new FinishedGood { Id = Guid.NewGuid(), TenantId = _tenantId, GskuId = gsku.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft, CanonicalCode = $"FG-{Guid.NewGuid():N}" };
        await _database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(globalProduct);
        await _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await _database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        await _database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        await _database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(finishedGood);

        var maker = Guid.NewGuid();
        var globalOperation = BaseGlobal(globalProduct.Id, maker);
        var firstOperation = BaseFirst(globalProduct.Id, revision.Id, gsku.Id, maker);
        var lskuOperation = BaseLsku(lsku.Id, gsku.Id, revision.Id, maker);
        var finishedOperation = BaseFinished(finishedGood.Id, gsku.Id, revision.Id, maker);
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(GlobalProductIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(globalOperation);
        await _database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(firstOperation);
        await _database.GetCollection<LskuIdentityWorkflowOperation>(LskuIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(lskuOperation);
        await _database.GetCollection<FinishedGoodIdentityWorkflowOperation>(FinishedGoodIdentityWorkflowOperationRepository.CollectionName).InsertOneAsync(finishedOperation);

        return
        [
            new(globalOperation.OperationId, new GlobalProductWorkflowRecoveryScope(globalProduct.Id, 0),
                [Audit(globalProduct.Id, AuditAggregateType.GlobalProduct)]),
            new(firstOperation.OperationId, new FirstGskuWorkflowRecoveryScope(globalProduct.Id, revision.Id, gsku.Id, 0, 0),
                [Audit(revision.Id, AuditAggregateType.ProductDefinitionRevision), Audit(gsku.Id, AuditAggregateType.Gsku)]),
            new(lskuOperation.OperationId, new LskuWorkflowRecoveryScope(lsku.Id, gsku.Id, revision.Id, "TR", 0),
                [Audit(lsku.Id, AuditAggregateType.Lsku)]),
            new(finishedOperation.OperationId, new FinishedGoodWorkflowRecoveryScope(finishedGood.Id, gsku.Id, revision.Id, 0),
                [Audit(finishedGood.Id, AuditAggregateType.FinishedGood)])
        ];
    }

    private ProductIdentityWorkflowOperationRecoveryMutation Mutation(
        ProductIdentityWorkflowOperationRecoveryCandidate candidate,
        ProductIdentityWorkflowOperationRecoveryScope scope,
        IReadOnlyList<LocalAuditIntent> intents,
        ProductIdentityWorkflowRecoveryDisposition disposition = ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart,
        ProductIdentityWorkflowOperationRecoverySuccessor? successor = null)
    {
        var evidence = Evidence(disposition);
        if (successor is not null) successor = successor with { MakerSubjectId = evidence.OperatorSubjectId };
        var operation = disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded
            ? ProductAuditOperation.ProductIdentityWorkflowOperationSuperseded
            : ProductAuditOperation.ProductIdentityWorkflowOperationAbandonedBeforeWorkflowStart;
        foreach (var intent in intents)
        {
            intent.Operation = operation;
            intent.ActorId = evidence.OperatorSubjectId.ToString("D");
            intent.CommandId = evidence.CommandId.ToString("D");
            intent.CausationId = evidence.CommandId.ToString("D");
        }
        return new(candidate.OperationId, candidate.OperationVersion, scope, candidate.OriginalMakerSubjectId,
            candidate.StartIdempotencyKey, candidate.OperationFingerprint, candidate.LeaseOwner,
            candidate.LeaseUntilUtcTicksV1, candidate.LeaseGeneration, evidence, successor, intents);
    }

    private ProductIdentityWorkflowOperationRecoveryEvidence Evidence(
        ProductIdentityWorkflowRecoveryDisposition disposition = ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        return new(disposition, Guid.NewGuid(), Guid.NewGuid(), "MAKER_UNAVAILABLE", null,
            Guid.NewGuid(), Hex('e'), now - 1, now);
    }

    private LocalAuditIntent Audit(Guid id, AuditAggregateType type)
    {
        var now = DateTimeOffset.UtcNow;
        return new() { IntentId = Guid.NewGuid(), TenantId = _tenantId, AggregateId = id,
            AggregateType = type, PreVersion = 0, PostVersion = 1, Sequence = 1,
            Operation = ProductAuditOperation.ProductIdentityWorkflowOperationAbandonedBeforeWorkflowStart,
            ActorId = Guid.NewGuid().ToString("D"), CorrelationId = Guid.NewGuid().ToString("D"),
            CausationId = Guid.NewGuid().ToString("D"), CommandId = Guid.NewGuid().ToString("D"),
            TimestampUtc = now, TimestampUtcTicksV1 = now.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = new string('A', 64), SnapshotReference = $"recovery/{id:D}",
            IdempotencyKey = $"recovery:{Guid.NewGuid():D}", DeliveryState = AuditIntentDeliveryState.Pending };
    }

    private async Task<int> AuditCountAsync(Fixture fixture) => fixture.Scope switch
    {
        GlobalProductWorkflowRecoveryScope scope => (await _database.GetCollection<GlobalProduct>("mdm_global_products").Find(x => x.TenantId == _tenantId && x.Id == scope.GlobalProductId).SingleAsync()).AuditIntents.Count,
        FirstGskuWorkflowRecoveryScope scope => (await _database.GetCollection<Gsku>("mdm_gskus").Find(x => x.TenantId == _tenantId && x.Id == scope.GskuId).SingleAsync()).AuditIntents.Count
            + (await _database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").Find(x => x.TenantId == _tenantId && x.Id == scope.ProductDefinitionRevisionId).SingleAsync()).AuditIntents.Count,
        LskuWorkflowRecoveryScope scope => (await _database.GetCollection<Lsku>("mdm_lskus").Find(x => x.TenantId == _tenantId && x.Id == scope.LskuId).SingleAsync()).AuditIntents.Count,
        FinishedGoodWorkflowRecoveryScope scope => (await _database.GetCollection<FinishedGood>("mdm_finished_goods").Find(x => x.TenantId == _tenantId && x.Id == scope.FinishedGoodId).SingleAsync()).AuditIntents.Count,
        _ => -1
    };

    private async Task<int> TargetVersionAsync(Fixture fixture) => fixture.Scope switch
    {
        GlobalProductWorkflowRecoveryScope scope => (await _database.GetCollection<GlobalProduct>("mdm_global_products").Find(x => x.TenantId == _tenantId && x.Id == scope.GlobalProductId).SingleAsync()).Version,
        FirstGskuWorkflowRecoveryScope scope => (await _database.GetCollection<Gsku>("mdm_gskus").Find(x => x.TenantId == _tenantId && x.Id == scope.GskuId).SingleAsync()).Version,
        LskuWorkflowRecoveryScope scope => (await _database.GetCollection<Lsku>("mdm_lskus").Find(x => x.TenantId == _tenantId && x.Id == scope.LskuId).SingleAsync()).Version,
        FinishedGoodWorkflowRecoveryScope scope => (await _database.GetCollection<FinishedGood>("mdm_finished_goods").Find(x => x.TenantId == _tenantId && x.Id == scope.FinishedGoodId).SingleAsync()).Version,
        _ => -1
    };

    private async Task<ProductIdentityLifecycleStatus> LifecycleAsync(Fixture fixture) => fixture.Scope switch
    {
        GlobalProductWorkflowRecoveryScope scope => (await _database.GetCollection<GlobalProduct>("mdm_global_products").Find(x => x.TenantId == _tenantId && x.Id == scope.GlobalProductId).SingleAsync()).LifecycleStatus,
        FirstGskuWorkflowRecoveryScope scope => (await _database.GetCollection<Gsku>("mdm_gskus").Find(x => x.TenantId == _tenantId && x.Id == scope.GskuId).SingleAsync()).LifecycleStatus,
        LskuWorkflowRecoveryScope scope => (await _database.GetCollection<Lsku>("mdm_lskus").Find(x => x.TenantId == _tenantId && x.Id == scope.LskuId).SingleAsync()).LifecycleStatus,
        FinishedGoodWorkflowRecoveryScope scope => (await _database.GetCollection<FinishedGood>("mdm_finished_goods").Find(x => x.TenantId == _tenantId && x.Id == scope.FinishedGoodId).SingleAsync()).LifecycleStatus,
        _ => default
    };

    private async Task<IReadOnlyList<Guid>> DiscoverAsync(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => (await new GlobalProductIdentityWorkflowOperationRepository(_database, new Tenant(_tenantId)).DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations.Select(x => x.OperationId).ToArray(),
        ProductIdentityWorkflowOperationFamily.FirstGsku => (await new FirstGskuIdentityWorkflowOperationRepository(_database, new Tenant(_tenantId)).DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations.Select(x => x.OperationId).ToArray(),
        ProductIdentityWorkflowOperationFamily.Lsku => (await new LskuIdentityWorkflowOperationRepository(_database, new Tenant(_tenantId)).DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations.Select(x => x.OperationId).ToArray(),
        ProductIdentityWorkflowOperationFamily.FinishedGood => (await new FinishedGoodIdentityWorkflowOperationRepository(_database, new Tenant(_tenantId)).DiscoverRecoverableAsync(DateTimeOffset.UtcNow.UtcTicks, 10)).Operations.Select(x => x.OperationId).ToArray(),
        _ => []
    };

    private void EnsureOperationIndexes(Guid tenantId)
    {
        _ = new GlobalProductIdentityWorkflowOperationRepository(_database, new Tenant(tenantId));
        _ = new FirstGskuIdentityWorkflowOperationRepository(_database, new Tenant(tenantId));
        _ = new LskuIdentityWorkflowOperationRepository(_database, new Tenant(tenantId));
        _ = new FinishedGoodIdentityWorkflowOperationRepository(_database, new Tenant(tenantId));
    }

    private GlobalProductIdentityWorkflowOperation BaseGlobal(Guid id, Guid maker) => new()
    { Id = Guid.NewGuid(), TenantId = _tenantId, OperationId = Guid.NewGuid(), GlobalProductId = id,
        ExpectedProductVersion = 0, MakerSubjectId = maker, StartIdempotencyKey = $"gp:{Guid.NewGuid():D}",
        OperationFingerprint = Hex('1'), Checkpoint = GlobalProductIdentityWorkflowCheckpoint.Prepared };
    private FirstGskuIdentityWorkflowOperation BaseFirst(Guid gp, Guid revision, Guid gsku, Guid maker) => new()
    { Id = Guid.NewGuid(), TenantId = _tenantId, OperationId = Guid.NewGuid(), GlobalProductId = gp,
        ProductDefinitionRevisionId = revision, GskuId = gsku, ExpectedRevisionVersion = 0, ExpectedGskuVersion = 0,
        MakerSubjectId = maker, StartIdempotencyKey = $"fg:{Guid.NewGuid():D}", OperationFingerprint = Hex('2'),
        Checkpoint = FirstGskuIdentityWorkflowCheckpoint.Prepared };
    private LskuIdentityWorkflowOperation BaseLsku(Guid lsku, Guid gsku, Guid revision, Guid maker) => new()
    { Id = Guid.NewGuid(), TenantId = _tenantId, OperationId = Guid.NewGuid(), LskuId = lsku, GskuId = gsku,
        ProductDefinitionRevisionId = revision, MarketCode = "TR", ExpectedLskuVersion = 0, MakerSubjectId = maker,
        StartIdempotencyKey = $"ls:{Guid.NewGuid():D}", OperationFingerprint = Hex('3'), Checkpoint = LskuIdentityWorkflowCheckpoint.Prepared };
    private FinishedGoodIdentityWorkflowOperation BaseFinished(Guid fg, Guid gsku, Guid revision, Guid maker) => new()
    { Id = Guid.NewGuid(), TenantId = _tenantId, OperationId = Guid.NewGuid(), FinishedGoodId = fg, GskuId = gsku,
        ProductDefinitionRevisionId = revision, ExpectedFinishedGoodVersion = 0, MakerSubjectId = maker,
        StartIdempotencyKey = $"fg:{Guid.NewGuid():D}", OperationFingerprint = Hex('4'), Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.Prepared };

    private static string Hex(char value) => new(value, 64);
    private sealed record Fixture(Guid OperationId, ProductIdentityWorkflowOperationRecoveryScope Scope,
        IReadOnlyList<LocalAuditIntent> AuditIntents);
    private sealed class Tenant(Guid tenantId) : ITenantContext
    { public Guid TenantId { get; private set; } = tenantId; public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId; }

    private static readonly string[] Collections =
    [
        "mdm_global_products", "mdm_product_definition_revisions", "mdm_gskus", "mdm_lskus", "mdm_finished_goods",
        GlobalProductIdentityWorkflowOperationRepository.CollectionName,
        FirstGskuIdentityWorkflowOperationRepository.CollectionName,
        LskuIdentityWorkflowOperationRepository.CollectionName,
        FinishedGoodIdentityWorkflowOperationRepository.CollectionName
    ];
}
