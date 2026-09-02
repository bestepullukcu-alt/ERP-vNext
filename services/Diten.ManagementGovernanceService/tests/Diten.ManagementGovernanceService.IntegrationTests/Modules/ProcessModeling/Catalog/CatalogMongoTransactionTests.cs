using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.Catalog;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling.Catalog;

[Collection(CatalogMongoCollection.Name)]
public sealed class CatalogMongoTransactionTests(DisposableCatalogMongo mongo)
{
    [Fact]
    public async Task Four_catalog_indexes_are_applied_once_to_the_single_fixed_database()
    {
        Assert.Equal("diten_mg_process_modeling_catalog_itest", DisposableCatalogMongo.DatabaseName);
        Assert.Equal(4, CatalogMongoManifest.Indexes.Count);
        Assert.All(CatalogMongoManifest.Indexes, index => Assert.Equal("TenantId", index.Keys[0]));

        foreach (var index in CatalogMongoManifest.Indexes)
        {
            using var cursor = await mongo.Context.Collection(index.Collection).Indexes.ListAsync();
            var names = (await cursor.ToListAsync()).Select(document => document["name"].AsString);
            Assert.Contains(index.Name, names);
        }
    }

    [Theory]
    [InlineData(CatalogMutationParticipant.Business)]
    [InlineData(CatalogMutationParticipant.Receipt)]
    [InlineData(CatalogMutationParticipant.AuditIntent)]
    [InlineData(CatalogMutationParticipant.Outbox)]
    public async Task Failure_at_each_participant_rolls_back_all_four(CatalogMutationParticipant failure)
    {
        var tenant = Guid.NewGuid();
        var store = new CatalogMongoStore(mongo.Context, participant =>
        {
            if (participant == failure) throw new InjectedParticipantFailure();
        });

        await Assert.ThrowsAsync<InjectedParticipantFailure>(() => store.MutateAsync(CreateArchitecture(tenant)));
        Assert.Equal(0, await CountTenantDocumentsAsync(tenant));
    }

    [Fact]
    public async Task Successful_mutation_persists_exactly_business_receipt_audit_and_outbox_and_replays_stably()
    {
        var tenant = Guid.NewGuid();
        var mutation = CreateArchitecture(tenant);
        var store = new CatalogMongoStore(mongo.Context);

        var created = await store.MutateAsync(mutation);
        var replayed = await store.MutateAsync(mutation);

        Assert.False(created.Replayed);
        Assert.True(replayed.Replayed);
        Assert.Equal(created.AggregateId, replayed.AggregateId);
        Assert.Equal(created.Version, replayed.Version);
        Assert.Equal(4, await CountTenantDocumentsAsync(tenant));
    }

    [Fact]
    public async Task Same_idempotency_key_with_different_payload_conflicts_without_extra_documents()
    {
        var tenant = Guid.NewGuid();
        var mutation = CreateArchitecture(tenant);
        var store = new CatalogMongoStore(mongo.Context);
        await store.MutateAsync(mutation);

        await Assert.ThrowsAsync<CatalogConflictException>(() =>
            store.MutateAsync(mutation with { PayloadHash = new string('b', 64) }));
        Assert.Equal(4, await CountTenantDocumentsAsync(tenant));
    }

    [Fact]
    public async Task Unknown_commit_result_retries_then_returns_unavailable_and_leaves_no_partial_commit()
    {
        var tenant = Guid.NewGuid();
        var attempts = 0;
        var store = new CatalogMongoStore(
            mongo.Context,
            testOnlyCommit: (_, _, _) =>
            {
                attempts++;
                return Task.FromException(new CatalogUnknownCommitException());
            });

        var exception = await Assert.ThrowsAsync<CatalogUnavailableException>(() => store.MutateAsync(CreateArchitecture(tenant)));
        Assert.Equal("process_modeling_catalog_commit_indeterminate", exception.Message);
        Assert.Equal(3, attempts);
        Assert.Equal(0, await CountTenantDocumentsAsync(tenant));
    }

    [Fact]
    public async Task Tenant_scoped_reads_cannot_disclose_another_tenants_record()
    {
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var mutation = CreateArchitecture(tenant);
        var store = new CatalogMongoStore(mongo.Context);
        await store.MutateAsync(mutation);

        Assert.NotNull(await store.FindByIdAsync(CatalogMongoCollections.Architectures, tenant, mutation.AggregateId));
        Assert.Null(await store.FindByIdAsync(CatalogMongoCollections.Architectures, otherTenant, mutation.AggregateId));
    }

    private static CatalogMongoMutation CreateArchitecture(Guid tenant) => new(
        tenant,
        Guid.NewGuid(),
        "management-governance.process-modeling.architectures.create",
        "CreateProcessArchitecture",
        "key-" + Guid.NewGuid().ToString("N"),
        new string('a', 64),
        Guid.NewGuid(),
        CatalogMongoCollections.Architectures,
        new BsonDocument
        {
            ["ArchitectureCode"] = "ORDER-TO-CASH",
            ["Name"] = "Order to Cash",
            ["Description"] = BsonNull.Value,
            ["SortOrder"] = 10,
            ["LifecycleState"] = "Active",
            ["CreatedAtUtc"] = DateTime.UtcNow
        },
        DateTime.UtcNow);

    private async Task<long> CountTenantDocumentsAsync(Guid tenant)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));
        long count = 0;
        foreach (var collection in CatalogMongoCollections.Business.Concat(new[]
                 { CatalogMongoCollections.Receipts, CatalogMongoCollections.AuditIntents, CatalogMongoCollections.Outbox }))
            count += await mongo.Context.Collection(collection).CountDocumentsAsync(filter);
        return count;
    }

    private sealed class InjectedParticipantFailure : Exception;
}
