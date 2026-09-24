using Diten.Platform.API.Configuration;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

[Collection(VerifiedMarketReplicaSetCollection.Name)]
public sealed class BusinessReferenceDataVerifiedMarketOperationalMongoTests
{
    private static readonly Guid ReferenceTenantId = Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId);
    private static readonly Guid HistoricalOwnerId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid ConsumerTenantId = Guid.Parse("74355e70-4c7d-410c-8cf6-db5fe3b9547f");
    private readonly VerifiedMarketReplicaSetFixture _fixture;

    public BusinessReferenceDataVerifiedMarketOperationalMongoTests(VerifiedMarketReplicaSetFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FreshPublishAndSameKeyReplay_CreateOneDurableMarketIdentityAndAuditIntent()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        var beforeArtifact = await File.ReadAllBytesAsync(harness.Facts.CatalogPath);
        await harness.SeedReadOnlyInvariantsAsync();
        var invariantsBefore = await harness.InvariantSnapshotAsync();

        await harness.RunAsync();
        var countsAfterFirst = await harness.TargetCountsAsync();
        await harness.RunAsync();
        var countsAfterReplay = await harness.TargetCountsAsync();

        Assert.Equal((1L, 1L, 1L, 1L, 25L, 0L, 0L), countsAfterFirst);
        Assert.Equal(countsAfterFirst, countsAfterReplay);
        Assert.Equal(invariantsBefore, await harness.InvariantSnapshotAsync());
        Assert.Equal(beforeArtifact, await File.ReadAllBytesAsync(harness.Facts.CatalogPath));
        var audit = await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .SingleAsync();
        var payload = audit["Payload"].AsBsonDocument;
        Assert.Equal(AuditActorType.PlatformAdministrator.ToString(), payload["ActorType"].AsString);
        Assert.Equal(
            Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedActorId),
            payload["ActorId"].AsBsonBinaryData.ToGuid());
        var metadata = payload["Metadata"].AsBsonDocument["_v"].AsBsonDocument;
        Assert.Equal(VerifiedMarketOperationalProvisioningOptions.LockedActorId, metadata["actor"].AsString);
        Assert.Equal(AuditActorType.PlatformAdministrator.ToString(), metadata["actorType"].AsString);
        var publication = await harness.ReadVerifiedPublicationAsync();
        Assert.NotNull(publication);
        Assert.Equal(249, publication.Version.Values.Count);
        Assert.Contains(publication.Version.Values, value => value.ValueCode == "TW");
    }

    [Fact]
    public async Task ForeignTargetTenant_IsRejectedBeforeAnyMarketOrAuditWrite()
    {
        var foreignFacts = VerifiedMarketOperationalReplicaHarness.LockedFacts() with
        {
            ReferenceTenantId = Guid.NewGuid()
        };
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(
            _fixture.Replica,
            facts: foreignFacts);
        await harness.SeedReadOnlyInvariantsAsync();
        var invariantsBefore = await harness.InvariantSnapshotAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
        Assert.Equal((0L, 0L, 0L, 0L, 0L, 0L, 0L), await harness.TargetCountsAsync());
        Assert.Equal(invariantsBefore, await harness.InvariantSnapshotAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("42b66b40-f47f-4d7b-9a90-15a654333d48")]
    public async Task NonCanonicalActor_IsRejectedBeforeAnyOperationalWrite(string actorId)
    {
        var facts = VerifiedMarketOperationalReplicaHarness.LockedFacts() with { ActorId = actorId };
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica, facts);
        await harness.SeedReadOnlyInvariantsAsync();
        var invariantsBefore = await harness.InvariantSnapshotAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
        Assert.Equal((0L, 0L, 0L, 0L, 0L, 0L, 0L), await harness.TargetCountsAsync());
        Assert.Equal(invariantsBefore, await harness.InvariantSnapshotAsync());
    }

    [Fact]
    public async Task MissingRequiredIndex_IsRejectedBeforeAnyMarketOrAuditWrite()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .Indexes.DropOneAsync("ux_audit_outbox_idempotency_key");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_REQUIRED_INDEX_MISMATCH", exception.Message);
        Assert.Equal((0L, 0L, 0L, 0L, 0L, 0L, 0L), await harness.TargetCountsAsync());
    }

    [Fact]
    public async Task WrongRequiredIndexSpec_IsRejectedBeforeAnyMarketOrAuditWrite()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        var indexes = harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox).Indexes;
        await indexes.DropOneAsync("ux_audit_outbox_idempotency_key");
        await indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("IdempotencyKey"),
            new CreateIndexOptions
            {
                Name = "ux_audit_outbox_idempotency_key",
                Unique = false
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_REQUIRED_INDEX_MISMATCH", exception.Message);
        Assert.Equal((0L, 0L, 0L, 0L, 0L, 0L, 0L), await harness.TargetCountsAsync());
    }

    [Fact]
    public async Task PreExistingTarget_IsManualReconciliationAndIsNotRetried()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
                ["TenantId"] = new BsonBinaryData(ReferenceTenantId, GuidRepresentation.Standard),
                ["SetCode"] = "market",
                ["IsDeleted"] = false
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_AMBIGUOUS", exception.Message);
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataPublishOperations)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataIntegrationEvents)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [InlineData("Market")]
    [InlineData("MARKET")]
    public async Task CaseVariantPreExistingMarket_IsRejectedBeforeAnyOperationalWrite(string setCode)
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
                ["TenantId"] = new BsonBinaryData(ReferenceTenantId, GuidRepresentation.Standard),
                ["SetCode"] = setCode,
                ["IsDeleted"] = false
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_AMBIGUOUS", exception.Message);
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataPublishOperations)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataIntegrationEvents)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task PartialCheckpoint_IsManualReconciliationWithoutAutomaticRecovery()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataVersions)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
                ["TenantId"] = new BsonBinaryData(ReferenceTenantId, GuidRepresentation.Standard),
                ["LastPublishIdempotencyKey"] = harness.OperationKey,
                ["IsDeleted"] = false
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_AMBIGUOUS", exception.Message);
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataIntegrationEvents)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task AuditAppendFailure_ReturnsManualReconciliationAndNeverClaimsExactReplay()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(
            _fixture.Replica,
            auditService: new RejectingAuditService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_MANUAL_RECONCILIATION_REQUIRED", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await harness.Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataIntegrationEvents)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var completion = await harness.VerifyCompletionAsync();
        Assert.Equal(VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired, completion.Disposition);
    }

    [Fact]
    public async Task PublishedMarketValueTamper_IsManualReconciliationWithoutAdditionalWriteOrRetry()
    {
        await using var harness = await VerifiedMarketOperationalReplicaHarness.CreateAsync(_fixture.Replica);
        await harness.RunAsync();
        var countsBeforeTamper = await harness.TargetCountsAsync();
        await harness.TamperOnePublishedValueAsync();

        var completion = await harness.VerifyCompletionAsync();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        Assert.Equal(VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired, completion.Disposition);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_PARTIAL_OR_MISMATCHED", completion.ReasonCode);
        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_TARGET_PARTIAL_OR_MISMATCHED", exception.Message);
        Assert.Equal(countsBeforeTamper, await harness.TargetCountsAsync());
    }

    private sealed class RejectingAuditService : IAuditService
    {
        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default) =>
            Task.FromResult(AuditAppendResult.Rejected("test-owned audit rejection"));
    }
}

internal sealed class VerifiedMarketOperationalReplicaHarness : IAsyncDisposable
{
    private static readonly Guid ReferenceTenantId = Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId);
    private static readonly Guid HistoricalOwnerId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid ConsumerTenantId = Guid.Parse("74355e70-4c7d-410c-8cf6-db5fe3b9547f");
    private readonly ServiceProvider _provider;

    private VerifiedMarketOperationalReplicaHarness(
        IMongoDatabase database,
        ServiceProvider provider,
        VerifiedMarketOperationalFacts facts)
    {
        Database = database;
        _provider = provider;
        Facts = facts;
    }

    public IMongoDatabase Database { get; }
    public VerifiedMarketOperationalFacts Facts { get; }
    public string OperationKey =>
        $"{Facts.IdempotencyNamespace}:businessreferencedata-catalog-v{Facts.CatalogVersion}:market".ToLowerInvariant();

    public static async Task<VerifiedMarketOperationalReplicaHarness> CreateAsync(
        Diten.Platform.Application.Tests.Persistence.DisposableMongoReplicaSet replica,
        VerifiedMarketOperationalFacts? facts = null,
        IAuditService? auditService = null)
    {
        var database = replica.CreateDatabase();
        await PlatformSchemaManifest.ApplyAsync(
            database,
            [SchemaProfile.BusinessReferenceData, SchemaProfile.AccessGovernance]);
        var databaseName = database.DatabaseNamespace.DatabaseName;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = replica.ConnectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                [$"{BusinessReferenceDataProviderOptions.SectionName}:ReferenceTenantId"] =
                    VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId
            })
            .Build();
        var lockedFacts = facts ?? LockedFacts();
        var eligibility = new TestEligibility(lockedFacts);
        var services = new ServiceCollection();
        services.AddVerifiedMarketOperationalPersistence(configuration);
        services.AddSingleton<IBusinessReferenceDataVerifiedMarketOperationalEligibility>(eligibility);
        services.AddScoped<VerifiedMarketOperationalProvisioningRunner>();
        if (auditService is not null)
        {
            services.AddSingleton(auditService);
            services.AddSingleton<IAuditService>(auditService);
        }

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        return new VerifiedMarketOperationalReplicaHarness(database, provider, lockedFacts);
    }

    public static VerifiedMarketOperationalFacts LockedFacts() => new(
        BusinessReferenceDataTestHarness.GetSeedPath(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName),
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion,
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint,
        ReferenceTenantId,
        VerifiedMarketOperationalProvisioningOptions.LockedActorId,
        "market-operational-replica-test");

    public async Task RunAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VerifiedMarketOperationalProvisioningRunner>().RunAsync();
    }

    public async Task<VerifiedMarketOperationalTargetPreflightResult> VerifyCompletionAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IVerifiedMarketOperationalPreflight>()
            .VerifyCompletionAsync(Facts);
    }

    public async Task<BusinessReferenceDataVerifiedPublication?> ReadVerifiedPublicationAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<Diten.Platform.Common.Tenancy.ITenantContext>()
            .SetTenant(Facts.ReferenceTenantId);
        return await scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataStewardshipRepository>()
            .GetVerifiedPublicationAsync("market", Facts.CatalogVersion, Facts.CatalogFingerprint);
    }

    public async Task TamperOnePublishedValueAsync()
    {
        var collection = Database.GetCollection<BusinessReferenceDataVersion>(PlatformCollections.BusinessReferenceDataVersions);
        var version = await collection
            .Find(value => value.TenantId == Facts.ReferenceTenantId && value.LastPublishIdempotencyKey == OperationKey)
            .SingleAsync();
        Assert.Equal(249, version.Values.Count);
        version.Values[0].DisplayName = $"{version.Values[0].DisplayName} [test-owned tamper]";
        var result = await collection.ReplaceOneAsync(
            value => value.Id == version.Id,
            version);
        Assert.Equal(1, result.ModifiedCount);
    }

    public async Task<(long Sets, long Versions, long Operations, long Audits, long Validations, long Assignments, long IntegrationEvents)> TargetCountsAsync()
    {
        var tenant = Facts.ReferenceTenantId;
        var sets = await Database.GetCollection<BusinessReferenceDataSet>(PlatformCollections.BusinessReferenceDataSets)
            .CountDocumentsAsync(value => value.TenantId == tenant && value.SetCode == "market");
        var versions = await Database.GetCollection<BusinessReferenceDataVersion>(PlatformCollections.BusinessReferenceDataVersions)
            .CountDocumentsAsync(value => value.TenantId == tenant && value.LastPublishIdempotencyKey == OperationKey);
        var operations = await Database.GetCollection<BusinessReferenceDataPublishOperation>(PlatformCollections.BusinessReferenceDataPublishOperations)
            .CountDocumentsAsync(value => value.TenantId == tenant && value.IdempotencyKey == OperationKey);
        var audits = await Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)));
        var validations = await Database.GetCollection<BusinessReferenceDataValidationResult>(PlatformCollections.BusinessReferenceDataValidationResults)
            .CountDocumentsAsync(value => value.TenantId == tenant);
        var assignments = await Database.GetCollection<BusinessReferenceDataTenantAssignment>(PlatformCollections.BusinessReferenceDataTenantAssignments)
            .CountDocumentsAsync(value => value.TenantId == tenant);
        var integrationEvents = await Database.GetCollection<BusinessReferenceDataIntegrationEvent>(PlatformCollections.BusinessReferenceDataIntegrationEvents)
            .CountDocumentsAsync(value => value.TenantId == tenant);
        return (sets, versions, operations, audits, validations, assignments, integrationEvents);
    }

    public async Task SeedReadOnlyInvariantsAsync()
    {
        await Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataSets)
            .InsertManyAsync([
                InvariantDocument("old-owner-market", HistoricalOwnerId, "market"),
                InvariantDocument("consumer-non-market", ConsumerTenantId, "currency"),
                InvariantDocument("owner-non-market", ReferenceTenantId, "country")]);
        await Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataTenantAssignments)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["TenantId"] = new BsonBinaryData(HistoricalOwnerId, GuidRepresentation.Standard),
                ["ConsumerTenantId"] = new BsonBinaryData(ConsumerTenantId, GuidRepresentation.Standard),
                ["SetCode"] = "market",
                ["TestInvariantMarker"] = "consumer-assignment",
                ["IsDeleted"] = false
            });
        await Database.GetCollection<BsonDocument>(PlatformCollections.BusinessReferenceDataValidationResults)
            .InsertOneAsync(new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["TenantId"] = new BsonBinaryData(HistoricalOwnerId, GuidRepresentation.Standard),
                ["BusinessReferenceDataVersionId"] = Guid.NewGuid().ToString("D"),
                ["RuleId"] = "RDV-001",
                ["TestInvariantMarker"] = "old-owner-validation",
                ["IsDeleted"] = false
            });
    }

    public async Task<string> InvariantSnapshotAsync()
    {
        var snapshots = new List<string>();
        foreach (var collectionName in new[]
                 {
                     PlatformCollections.BusinessReferenceDataSets,
                     PlatformCollections.BusinessReferenceDataVersions,
                     PlatformCollections.BusinessReferenceDataPublishOperations,
                     PlatformCollections.BusinessReferenceDataTenantAssignments,
                     PlatformCollections.BusinessReferenceDataValidationResults
                 })
        {
            var documents = await Database.GetCollection<BsonDocument>(collectionName)
                .Find(Builders<BsonDocument>.Filter.Exists("TestInvariantMarker"))
                .Sort(Builders<BsonDocument>.Sort.Ascending("TestInvariantMarker"))
                .ToListAsync();
            snapshots.Add($"{collectionName}:{string.Join('|', documents.Select(document => document.ToJson()))}");
        }

        return string.Join('\n', snapshots);
    }

    private static BsonDocument InvariantDocument(string marker, Guid tenantId, string setCode) => new()
    {
        ["_id"] = ObjectId.GenerateNewId(),
        ["TenantId"] = new BsonBinaryData(tenantId, GuidRepresentation.Standard),
        ["SetCode"] = setCode,
        ["TestInvariantMarker"] = marker,
        ["IsDeleted"] = false
    };

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    private sealed class TestEligibility : IBusinessReferenceDataVerifiedMarketOperationalEligibility
    {
        private readonly VerifiedMarketOperationalFacts _facts;
        private readonly Authorization _authorization = new();

        public TestEligibility(VerifiedMarketOperationalFacts facts)
        {
            _facts = facts;
        }

        public Task<VerifiedMarketOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct = default) =>
            Task.FromResult(new VerifiedMarketOperationalEligibilityDecision(
                true,
                "VERIFIED_MARKET_OPERATIONAL_ELIGIBLE",
                _facts,
                _authorization));

        public bool IsAuthorized(
            IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,
            VerifiedMarketOperationalFacts facts) =>
            ReferenceEquals(authorization, _authorization) && facts == _facts;

        private sealed class Authorization : IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
    }
}
