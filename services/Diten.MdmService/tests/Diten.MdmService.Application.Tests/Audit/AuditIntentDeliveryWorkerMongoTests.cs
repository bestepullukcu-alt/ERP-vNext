using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuditIntentDeliveryWorkerMongoTests(AuditIntentTemporalMongoFixture mongo)
    : IAsyncLifetime, IClassFixture<AuditIntentTemporalMongoFixture>
{
    private const string DatabaseName = "diten_mdm_audit_worker_itest";
    private string _databaseName = null!;
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        // DB-010: this database-global discovery test owns an isolated mongod fixture,
        // so it uses one fixed suffix rather than creating a database per run.
        _databaseName = DatabaseName;
        var settings = MongoClientSettings.FromConnectionString(mongo.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        _database = new MongoClient(settings).GetDatabase(_databaseName);
        var now = DateTimeOffset.UtcNow;
        await _database.GetCollection<AuditIntentTemporalMigrationState>(AuditIntentTemporalMigrationRepository.StateCollectionName)
            .InsertOneAsync(new AuditIntentTemporalMigrationState
            {
                Phase = AuditIntentTemporalMigrationState.Phases.CutoverActive,
                CollectionOrdinal = 8,
                CollectionBoundaries = Enumerable.Range(0, 8).Select(index => new AuditIntentTemporalCollectionBoundary
                {
                    CollectionOrdinal = index,
                    UpperAggregateId = Guid.Empty
                }).ToList(),
                UpdatedAtUtcTicks = now.UtcTicks,
                CompletedAtUtcTicks = now.UtcTicks,
                ActivationVersion = AuditIntentTemporalStorage.CurrentVersion,
                IndexEvidenceFingerprint = AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint
            });
    }

    public Task DisposeAsync() => new MongoClient(mongo.ReplicaConnectionString).DropDatabaseAsync(_databaseName);

    [Fact]
    public async Task Tenant_discovery_is_bounded_cursor_paged_and_deduplicated_across_collections()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("20000000-0000-0000-0000-000000000002");
        foreach (var (collection, type) in AggregateCollections)
        {
            await InsertAsync(collection, type, first);
        }
        await InsertAsync("mdm_lskus", AuditAggregateType.Lsku, second);
        var repository = new AuditIntentTenantPartitionDiscoveryRepository(
            _database,
            new AuditIntentTemporalMigrationRepository(_database, TimeProvider.System),
            TimeProvider.System);

        var pageOne = await repository.DiscoverAsync(null, 1);
        var pageTwo = await repository.DiscoverAsync(pageOne.NextTenantId, 1);
        var done = await repository.DiscoverAsync(pageTwo.NextTenantId, 1);

        Assert.Equal([first], pageOne.TenantIds);
        Assert.Equal([second], pageTwo.TenantIds);
        Assert.Empty(done.TenantIds);
        Assert.Null(done.NextTenantId);
    }


    [Fact]
    public async Task Abb_only_tenant_discovery_reaches_existing_delivery_without_cross_tenant_exposure()
    {
        var tenantId = Guid.NewGuid();
        var entry = CreateAbb(tenantId);
        await _database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .InsertOneAsync(entry);
        var discovery = Discovery();
        Assert.Equal([tenantId], (await discovery.DiscoverAsync(null, 10)).TenantIds);
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var delivery = new AuditIntentDeliveryRepository(_database, tenant, TimeProvider.System);
        var work = Assert.Single(await delivery.DiscoverEligibleAsync(10));
        Assert.Equal(entry.Id, work.Locator.AggregateId);
        Assert.Equal(AuditAggregateType.ProductAbbreviation, work.Locator.AggregateType);
        var foreign = new TenantContext();
        foreign.SetTenant(Guid.NewGuid());
        var foreignDelivery = new AuditIntentDeliveryRepository(_database, foreign, TimeProvider.System);
        Assert.Empty(await foreignDelivery.DiscoverEligibleAsync(10));
        Assert.Null(await foreignDelivery.TryClaimAsync(work.Locator, 0, "foreign", TimeSpan.FromMinutes(1)));
        var claim = Assert.IsType<AuditIntentClaim>(
            await delivery.TryClaimAsync(work.Locator, 0, "test-owner", TimeSpan.FromMinutes(1)));
        var payload = Assert.IsType<AuditIntentClaimedPayload>(await delivery.ReadClaimedPayloadAsync(claim));
        Assert.Equal(tenantId, payload.TenantId);
        Assert.Equal(ProductAuditOperation.ProductAbbreviationAllocationRequested, payload.Operation);
    }

    [Fact]
    public async Task Abb_and_original_eight_collections_share_bounded_deduplicated_cursor_pages()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("20000000-0000-0000-0000-000000000002");
        foreach (var (collection, type) in AggregateCollections)
            await InsertAsync(collection, type, first);
        await _database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .InsertManyAsync([CreateAbb(first), CreateAbb(first), CreateAbb(second)]);
        var discovery = Discovery();
        var page1 = await discovery.DiscoverAsync(null, 1);
        var page2 = await discovery.DiscoverAsync(page1.NextTenantId, 1);
        var page3 = await discovery.DiscoverAsync(page2.NextTenantId, 1);
        Assert.Equal([first], page1.TenantIds);
        Assert.Equal([second], page2.TenantIds);
        Assert.Empty(page3.TenantIds);
        Assert.Null(page3.NextTenantId);
    }

    [Theory]
    [InlineData("foreign-intent-tenant")]
    [InlineData("wrong-aggregate")]
    [InlineData("wrong-source")]
    [InlineData("legacy-temporal")]
    [InlineData("future-retry")]
    [InlineData("active-lease")]
    [InlineData("delivered")]
    [InlineData("dead-letter")]
    public async Task Abb_ineligible_intents_are_not_discovered(string scenario)
    {
        var entry = CreateAbb(Guid.NewGuid());
        var intent = Assert.Single(entry.AuditIntents);
        switch (scenario)
        {
            case "foreign-intent-tenant": intent.TenantId = Guid.NewGuid(); break;
            case "wrong-aggregate": intent.AggregateType = AuditAggregateType.Gsku; break;
            case "wrong-source": intent.SourceService = "untrusted-test-source"; break;
            case "legacy-temporal": intent.TemporalStorageVersion = null; break;
            case "future-retry":
                intent.NextRetryAt = DateTimeOffset.UtcNow.AddHours(1);
                intent.NextRetryAtUtcTicksV1 = intent.NextRetryAt.Value.UtcTicks;
                break;
            case "active-lease":
                intent.DeliveryState = AuditIntentDeliveryState.Processing;
                intent.LeaseUntil = DateTimeOffset.UtcNow.AddHours(1);
                intent.LeaseUntilUtcTicksV1 = intent.LeaseUntil.Value.UtcTicks;
                break;
            case "delivered": intent.DeliveryState = AuditIntentDeliveryState.Delivered; break;
            case "dead-letter": intent.DeliveryState = AuditIntentDeliveryState.DeadLetter; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        await _database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .InsertOneAsync(entry);
        Assert.Empty((await Discovery().DiscoverAsync(null, 10)).TenantIds);
    }

    private AuditIntentTenantPartitionDiscoveryRepository Discovery() => new(
        _database, new AuditIntentTemporalMigrationRepository(_database, TimeProvider.System), TimeProvider.System);

    private static ProductAbbreviationRegisterEntry CreateAbb(Guid tenantId)
    {
        var entry = new ProductAbbreviationRegisterEntry
        {
            TenantId = tenantId, Version = 1, NormalizedAbbreviation = "TST", GlobalProductId = Guid.NewGuid()
        };
        var intent = new LocalAuditIntent
        {
            TenantId = tenantId, AggregateId = entry.Id, AggregateType = AuditAggregateType.ProductAbbreviation,
            IntentId = Guid.NewGuid(), Operation = ProductAuditOperation.ProductAbbreviationAllocationRequested,
            ContractVersion = "mod-0290.audit-intent.v1", TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ActorId = Guid.NewGuid().ToString("D"), CorrelationId = Guid.NewGuid().ToString("D"),
            CommandId = Guid.NewGuid().ToString("D"), CausationId = Guid.NewGuid().ToString("D"),
            IdempotencyKey = Guid.NewGuid().ToString("D"), EvidenceHash = new string('A', 64),
            PreVersion = 0, PostVersion = 1, Sequence = 1
        };
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);
        entry.AuditIntents.Add(intent);
        return entry;
    }

    private static readonly (string Collection, AuditAggregateType Type)[] AggregateCollections =
    [
        ("mdm_code_reservations", AuditAggregateType.CodeReservation),
        ("mdm_global_products", AuditAggregateType.GlobalProduct),
        ("mdm_product_definition_revisions", AuditAggregateType.ProductDefinitionRevision),
        ("mdm_gskus", AuditAggregateType.Gsku),
        ("mdm_finished_goods", AuditAggregateType.FinishedGood),
        ("mdm_lskus", AuditAggregateType.Lsku),
        ("mdm_product_legal_entity_scope_policies", AuditAggregateType.ProductLegalEntityScopePolicy),
        ("mdm_product_legal_entity_scope_rollout_states", AuditAggregateType.ProductLegalEntityScopeRolloutState)
    ];

    private Task InsertAsync(string collection, AuditAggregateType type, Guid tenantId)
    {
        var aggregateId = Guid.NewGuid();
        return _database.GetCollection<BsonDocument>(collection).InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(aggregateId, GuidRepresentation.Standard),
            ["TenantId"] = new BsonBinaryData(tenantId, GuidRepresentation.Standard),
            ["AuditIntents"] = new BsonArray
            {
                new BsonDocument
                {
                    ["IntentId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
                    ["TenantId"] = new BsonBinaryData(tenantId, GuidRepresentation.Standard),
                    ["AggregateId"] = new BsonBinaryData(aggregateId, GuidRepresentation.Standard),
                    ["AggregateType"] = (int)type,
                    ["SourceService"] = AuditIntentContract.SourceService,
                    ["TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion,
                    ["DeliveryState"] = (int)AuditIntentDeliveryState.Pending,
                    ["NextRetryAtUtcTicksV1"] = BsonNull.Value,
                    ["LeaseUntilUtcTicksV1"] = BsonNull.Value
                }
            }
        });
    }
}
