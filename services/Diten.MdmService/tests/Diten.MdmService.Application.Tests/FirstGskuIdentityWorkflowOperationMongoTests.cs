using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class FirstGskuIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<FirstGskuIdentityWorkflowOperation> _collection = null!;

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
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _collection = _database.GetCollection<FirstGskuIdentityWorkflowOperation>(
            FirstGskuIdentityWorkflowOperationRepository.CollectionName);
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_pair_scoped_and_tenant_isolated()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("pair-key", "fingerprint-a");
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(Operation(
            "pair-key", "fingerprint-a", operation.OperationId,
            operation.ProductDefinitionRevisionId, operation.GskuId));
        var drift = await repository.ReserveAsync(Operation(
            "pair-key", "fingerprint-b", operation.OperationId,
            operation.ProductDefinitionRevisionId, operation.GskuId));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Null(await Repository(_tenantBId).GetByOperationIdAsync(operation.OperationId));
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Claim_checkpoint_proofs_are_scoped_and_maker_replay_can_persist_exact_start_proof()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("claim", "claim-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.Prepared], $"worker-{i}", now,
            now + TimeSpan.FromMinutes(1).Ticks))));
        var winner = Assert.Single(claims, item => item is not null)!;
        Assert.False(await repository.AdvanceAsync(winner, new(
            FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ApprovalPackApplicabilitySelection: Selection("pack-applicability", operation.PackApplicabilityCode),
            ApprovalPackUomSelection: Selection("uom", operation.PackUomCode),
            ReferencesValidatedAtUtcTicksV1: now,
            ApprovalReferenceProofFingerprint: "proof-a",
            ReleaseLease: true)));
        Assert.True(await repository.AdvanceAsync(winner, new(
            FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1, ReleaseLease: true)));
        var unknown = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown], "lookup", now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(unknown);
        Assert.True(await repository.AdvanceAsync(unknown!, new(
            FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now + 3, ReleaseLease: true)));
        var makerReplay = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay], "maker", now + 4,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(makerReplay);
        Assert.True(await repository.AdvanceAsync(makerReplay!, new(
            FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 5,
            WorkflowInstanceId: Guid.NewGuid(), WorkflowTemplateId: Guid.NewGuid(),
            WorkflowTemplateVersionId: Guid.NewGuid(), ApprovalTaskId: Guid.NewGuid(),
            AssignmentSnapshotId: Guid.NewGuid(), StartTransitionLogId: Guid.NewGuid(),
            WorkflowStartedAtUtcTicksV1: now + 5, ReleaseLease: true)));
        Assert.False(await repository.AdvanceAsync(makerReplay, new(
            FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 6,
            WorkflowInstanceId: Guid.NewGuid())));

        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted, stored!.Checkpoint);
        Assert.NotNull(stored.WorkflowInstanceId);
        Assert.Null(stored.ApprovalReferenceProofFingerprint);
        Assert.Null(stored.LeaseOwner);
    }

    [Fact]
    public async Task Recoverable_discovery_excludes_active_lease_and_pages_null_then_same_tick_by_operation_id()
    {
        var repository = Repository(_tenantId);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var nullDue = Operation("null", "fp-null", Guid.Parse("01000000-0000-0000-0000-000000000001"));
        var first = Operation("first", "fp-first", Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var second = Operation("second", "fp-second", Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var leased = Operation("leased", "fp-leased");
        foreach (var item in new[] { nullDue, first, second, leased }) Assert.True((await repository.ReserveAsync(item)).Succeeded);
        await _collection.UpdateManyAsync(x => x.TenantId == _tenantId && new[] { first.OperationId, second.OperationId }.Contains(x.OperationId),
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now));
        await _collection.UpdateOneAsync(x => x.TenantId == _tenantId && x.OperationId == leased.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now)
                .Set(x => x.LeaseOwner, "active").Set(x => x.LeaseUntilUtcTicksV1, now + 1000));

        var p1 = await repository.DiscoverRecoverableAsync(now, 1);
        var p2 = await repository.DiscoverRecoverableAsync(now, 1, p1.NextCursor);
        var p3 = await repository.DiscoverRecoverableAsync(now, 1, p2.NextCursor);
        Assert.Equal(nullDue.OperationId, Assert.Single(p1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(p2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(p3.Operations).OperationId);
    }

    [Fact]
    public async Task Approval_proof_is_durable_before_revision_mutation_and_cannot_be_overwritten_after_crash()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("approval-proof", "approval-proof-fingerprint");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        await _collection.UpdateOneAsync(
            item => item.TenantId == _tenantId && item.OperationId == operation.OperationId,
            Builders<FirstGskuIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
                .Set(item => item.DecisionKind, ProductIdentityDecisionKind.Approved));

        var decisionClaim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.DecisionObserved],
            "approval-validator",
            now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(decisionClaim);
        var originalPack = Selection("pack-applicability", operation.PackApplicabilityCode);
        var originalUom = Selection("uom", operation.PackUomCode);
        var originalProof = new string('a', 64);
        Assert.True(await repository.AdvanceAsync(decisionClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 1,
            ApprovalPackApplicabilitySelection: originalPack,
            ApprovalPackUomSelection: originalUom,
            ReferencesValidatedAtUtcTicksV1: now + 1,
            ApprovalReferenceProofFingerprint: originalProof,
            ReleaseLease: true)));

        var afterCrash = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated, afterCrash!.Checkpoint);
        Assert.Equal(originalProof, afterCrash.ApprovalReferenceProofFingerprint);
        Assert.Equal(originalPack.ValueCode, afterCrash.ApprovalPackApplicabilitySelection!.ValueCode);
        Assert.Equal(originalUom.ValueCode, afterCrash.ApprovalPackUomSelection!.ValueCode);

        var recoveryClaim = await repository.TryClaimAsync(new(
            operation.OperationId,
            operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated],
            "approval-recovery",
            now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(recoveryClaim);
        Assert.False(await repository.AdvanceAsync(recoveryClaim!, new(
            FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 3,
            ApprovalPackApplicabilitySelection: Selection("pack-applicability", "DRIFT"),
            ApprovalPackUomSelection: originalUom,
            ReferencesValidatedAtUtcTicksV1: now + 3,
            ApprovalReferenceProofFingerprint: new string('b', 64))));
        Assert.True(await repository.AdvanceAsync(recoveryClaim, new(
            FirstGskuIdentityWorkflowCheckpoint.RevisionApproved,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now + 3,
            ReleaseLease: true)));

        var completedRevisionStep = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.RevisionApproved, completedRevisionStep!.Checkpoint);
        Assert.Equal(originalProof, completedRevisionStep.ApprovalReferenceProofFingerprint);
        Assert.Equal(originalPack.ValueCode, completedRevisionStep.ApprovalPackApplicabilitySelection!.ValueCode);
    }

    [Fact]
    public async Task Repository_owns_exact_four_tenant_safe_indexes_and_no_secret_fields()
    {
        var operation = Operation("shape", "shape-fingerprint");
        Assert.True((await Repository(_tenantId).ReserveAsync(operation)).Succeeded);
        var indexes = await (await _collection.Indexes.ListAsync()).ToListAsync();
        var owned = indexes.Where(x => x["name"].AsString.Contains("first_gsku_identity_workflow", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, owned.Length);
        Assert.All(owned, index => Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name));
        var raw = await _database.GetCollection<BsonDocument>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(new BsonDocument("OperationId", new BsonBinaryData(operation.OperationId, GuidRepresentation.Standard))).SingleAsync();
        Assert.True(raw["CreatedAtUtcTicksV1"].IsInt64);
        Assert.DoesNotContain(raw.Names, n => n.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Credential", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
    }

    public async Task DisposeAsync() => await _collection.DeleteManyAsync(
        x => x.TenantId == _tenantId || x.TenantId == _tenantBId);

    private FirstGskuIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private FirstGskuIdentityWorkflowOperation Operation(
        string key, string fingerprint, Guid? operationId = null, Guid? revisionId = null, Guid? gskuId = null)
    {
        var revision = revisionId ?? Guid.NewGuid();
        var gsku = gskuId ?? Guid.NewGuid();
        return new()
        {
            TenantId = _tenantId, OperationId = operationId ?? Guid.NewGuid(),
            ProductDefinitionRevisionId = revision, GskuId = gsku, GlobalProductId = Guid.NewGuid(),
            CreationCommandId = $"create-{key}", ExpectedRevisionVersion = 0, ExpectedGskuVersion = 0,
            MakerSubjectId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "FIRST_GSKU_IDENTITY_APPROVAL",
            ObjectType = "gsku", ObjectId = gsku.ToString("D"), ObjectRef = $"GS:{gsku:D}",
            StartIdempotencyKey = key, OperationFingerprint = fingerprint,
            PackApplicabilityCode = "PACK", PackQuantity = 1, PackUomCode = "EA",
            PackApplicabilitySelection = Selection("pack-applicability", "PACK"),
            PackUomSelection = Selection("uom", "EA"), CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private static ReferenceCatalogSelection Selection(string set, string code) => new()
    {
        SetCode = set, ValueCode = code, CatalogVersionId = Guid.NewGuid(), CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Pinned,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
