using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
public sealed class LskuIdentityWorkflowOperationMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<LskuIdentityWorkflowOperation> _collection = null!;

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
        _collection = _database.GetCollection<LskuIdentityWorkflowOperation>(
            LskuIdentityWorkflowOperationRepository.CollectionName);
        _ = Repository(_tenantId);
    }

    [Fact]
    public async Task Reserve_is_exactly_replayable_and_tenant_isolated()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("reserve", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var first = await repository.ReserveAsync(operation);
        var replay = await repository.ReserveAsync(ReplayOf(operation));
        var drifted = ReplayOf(operation);
        drifted.OperationFingerprint =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var drift = await repository.ReserveAsync(drifted);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.False(drift.Succeeded);
        Assert.Equal("LSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT", drift.ErrorCode);
        Assert.Null(await Repository(_otherTenantId).GetByOperationIdAsync(operation.OperationId));
        Assert.Single(await _collection.Find(x => x.TenantId == _tenantId).ToListAsync());
    }

    [Fact]
    public async Task Claim_has_one_winner_and_expired_lease_increments_generation()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("claim", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero).UtcTicks;
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], $"worker-{index}", now,
            now + TimeSpan.FromMinutes(1).Ticks))));
        var winner = Assert.Single(claims, value => value is not null)!;
        Assert.Equal(1, winner.LeaseGeneration);
        Assert.Null(await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], "early", now + 1,
            now + TimeSpan.FromMinutes(1).Ticks)));
        var recovered = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.Prepared], "recovered",
            now + TimeSpan.FromMinutes(1).Ticks + 1,
            now + TimeSpan.FromMinutes(2).Ticks));
        Assert.NotNull(recovered);
        Assert.Equal(2, recovered!.LeaseGeneration);
    }

    [Fact]
    public async Task Approval_proof_is_atomic_exact_and_immutable_after_crash()
    {
        var repository = Repository(_tenantId);
        var operation = Operation("approval", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var sequence = 7L;
        await _collection.UpdateOneAsync(
            x => x.TenantId == _tenantId && x.OperationId == operation.OperationId,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.DecisionObserved)
                .Set(x => x.DecisionKind, ProductIdentityDecisionKind.Approved)
                .Set(x => x.DecisionTransitionSequence, sequence));
        var claim = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.DecisionObserved], "validator", now,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);
        var selection = Selection();
        var proof = ApprovalProof(operation, sequence, selection, now + 1);
        Assert.True(await repository.AdvanceAsync(claim!, new(
            LskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1,
            ApprovalMarketSelection: selection,
            MarketValidatedAtUtcTicksV1: now + 1,
            ApprovalMarketProofFingerprint: proof,
            ReleaseLease: true)));
        var stored = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ApprovalValidated, stored!.Checkpoint);
        Assert.Equal(proof, stored.ApprovalMarketProofFingerprint);
        var recovery = await repository.TryClaimAsync(new(
            operation.OperationId, operation.OperationFingerprint,
            [LskuIdentityWorkflowCheckpoint.ApprovalValidated], "recovery", now + 2,
            now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(recovery);
        Assert.False(await repository.AdvanceAsync(recovery!, new(
            LskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 3,
            ApprovalMarketSelection: Selection(), MarketValidatedAtUtcTicksV1: now + 3,
            ApprovalMarketProofFingerprint: new string('e', 64), ReleaseLease: true)));
        var unchanged = await repository.GetByOperationIdAsync(operation.OperationId);
        Assert.Equal(proof, unchanged!.ApprovalMarketProofFingerprint);
    }

    [Fact]
    public async Task Recovery_order_is_null_then_due_tick_then_operation_id_and_indexes_are_tenant_first()
    {
        var repository = Repository(_tenantId);
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var nullDue = Operation("null", new string('1', 64), Guid.Parse("01000000-0000-0000-0000-000000000001"));
        var first = Operation("first", new string('2', 64), Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var second = Operation("second", new string('3', 64), Guid.Parse("20000000-0000-0000-0000-000000000001"));
        foreach (var item in new[] { nullDue, first, second })
            Assert.True((await repository.ReserveAsync(item)).Succeeded);
        await _collection.UpdateManyAsync(
            x => x.TenantId == _tenantId && new[] { first.OperationId, second.OperationId }.Contains(x.OperationId),
            Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, now));
        var p1 = await repository.DiscoverRecoverableAsync(now, 1);
        var p2 = await repository.DiscoverRecoverableAsync(now, 1, p1.NextCursor);
        var p3 = await repository.DiscoverRecoverableAsync(now, 1, p2.NextCursor);
        Assert.Equal(nullDue.OperationId, Assert.Single(p1.Operations).OperationId);
        Assert.Equal(first.OperationId, Assert.Single(p2.Operations).OperationId);
        Assert.Equal(second.OperationId, Assert.Single(p3.Operations).OperationId);

        var indexes = await (await _collection.Indexes.ListAsync()).ToListAsync();
        var owned = indexes.Where(x => x["name"].AsString.Contains("lsku_identity_workflow", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, owned.Length);
        Assert.All(owned, index => Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name));
    }

    public async Task DisposeAsync() => await _collection.DeleteManyAsync(
        x => x.TenantId == _tenantId || x.TenantId == _otherTenantId);

    private LskuIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private LskuIdentityWorkflowOperation Operation(
        string key, string fingerprint, Guid? operationId = null, Guid? lskuId = null,
        Guid? gskuId = null, Guid? revisionId = null, ReferenceCatalogSelection? selection = null)
    {
        var lsku = lskuId ?? Guid.NewGuid();
        var operation = operationId ?? Guid.NewGuid();
        return new()
        {
            Id = operation, TenantId = _tenantId,
            OperationId = operation, LskuId = lsku,
            GskuId = gskuId ?? Guid.NewGuid(), ProductDefinitionRevisionId = revisionId ?? Guid.NewGuid(),
            ExpectedLskuVersion = 0, MakerSubjectId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "LSKU_IDENTITY_APPROVAL",
            ObjectType = "lsku", ObjectId = lsku.ToString("D"), ObjectRef = $"LS:{lsku:D}",
            StartIdempotencyKey = $"lsku-{key}", OperationFingerprint = fingerprint,
            MarketCode = "TR", MarketSelection = selection ?? Selection(),
            CreatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks
        };
    }

    private static ReferenceCatalogSelection Selection() => new()
    {
        SetCode = "market", ValueCode = "TR", CatalogVersionId = Guid.NewGuid(),
        CatalogVersionNumber = 1, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private static string ApprovalProof(
        LskuIdentityWorkflowOperation operation, long sequence,
        ReferenceCatalogSelection selection, long validatedAt)
    {
        var facts = string.Join('|', "lsku-approval-market-v1", operation.TenantId.ToString("D"),
            operation.OperationId.ToString("D"), operation.LskuId.ToString("D"), operation.MarketCode,
            sequence.ToString(CultureInfo.InvariantCulture), selection.SetCode, selection.ValueCode,
            selection.CatalogVersionId.ToString("D"),
            selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
            ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture),
            selection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            validatedAt.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static LskuIdentityWorkflowOperation ReplayOf(LskuIdentityWorkflowOperation source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, OperationId = source.OperationId,
        LskuId = source.LskuId, GskuId = source.GskuId,
        ProductDefinitionRevisionId = source.ProductDefinitionRevisionId,
        ExpectedLskuVersion = source.ExpectedLskuVersion, MakerSubjectId = source.MakerSubjectId,
        WorkflowTemplateId = source.WorkflowTemplateId, WorkflowTemplateCode = source.WorkflowTemplateCode,
        CandidatePrincipalIds = [.. source.CandidatePrincipalIds], ReasonCode = source.ReasonCode,
        CommentRequired = source.CommentRequired, EvidenceRequired = source.EvidenceRequired,
        DueAtUtcTicksV1 = source.DueAtUtcTicksV1, ObjectType = source.ObjectType,
        ObjectId = source.ObjectId, ObjectRef = source.ObjectRef,
        StartIdempotencyKey = source.StartIdempotencyKey,
        OperationFingerprint = source.OperationFingerprint, MarketCode = source.MarketCode,
        MarketSelection = source.MarketSelection, CreatedAtUtcTicksV1 = source.CreatedAtUtcTicksV1
    };

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
