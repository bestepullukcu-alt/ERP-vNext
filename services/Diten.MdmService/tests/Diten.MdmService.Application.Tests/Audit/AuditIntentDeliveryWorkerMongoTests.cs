using Diten.MdmService.Domain.Entities;
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
        _database = new MongoClient(mongo.ReplicaConnectionString).GetDatabase(_databaseName);
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
