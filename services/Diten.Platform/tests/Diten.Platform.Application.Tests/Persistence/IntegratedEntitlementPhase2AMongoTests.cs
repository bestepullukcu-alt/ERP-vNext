using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class IntegratedEntitlementPhase2AMongoTests
{
    private const string ModuleCode = "PPM";
    private const string StateCollection = "phase2a_integrated_state";
    private const string IntegrationOutbox = "outbox_events";
    private const string AuditOutbox = "audit_outbox";

    [Fact]
    public async Task ThreeComponentVector_IsolatedMutationsNoOpAndRollback_AreAtomic()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        await ProvisionCollectionsAsync(database);
        var context = new PlatformDbContext(mongo.Client, database);
        var executor = new PlatformTransactionExecutor(context);
        var versions = new EntitlementStateVersionRepository(context);
        var tenantId = Guid.NewGuid();

        Assert.Equal(new VersionVector(0, 0, 0), await ReadVectorAsync(database, tenantId));

        await ExecuteFamilyAsync(executor, versions, context, tenantId, "physical");
        Assert.Equal(new VersionVector(1, 0, 0), await ReadVectorAsync(database, tenantId));

        await ExecuteFamilyAsync(executor, versions, context, tenantId, "subscription");
        Assert.Equal(new VersionVector(1, 1, 0), await ReadVectorAsync(database, tenantId));

        await ExecuteFamilyAsync(executor, versions, context, tenantId, "global");
        Assert.Equal(new VersionVector(1, 1, 1), await ReadVectorAsync(database, tenantId));
        Assert.Equal(3, await CountAsync(database, StateCollection));
        Assert.Equal(3, await CountAsync(database, IntegrationOutbox));
        Assert.Equal(3, await CountAsync(database, AuditOutbox));

        await executor.ExecuteAsync((_, _) => Task.FromResult(0));
        Assert.Equal(new VersionVector(1, 1, 1), await ReadVectorAsync(database, tenantId));
        Assert.Equal(3, await CountAsync(database, StateCollection));
        Assert.Equal(3, await CountAsync(database, IntegrationOutbox));
        Assert.Equal(3, await CountAsync(database, AuditOutbox));

        var rollbackTenantId = Guid.NewGuid();
        var rollbackBaseline = await ReadVectorAsync(database, rollbackTenantId);
        await Assert.ThrowsAsync<InjectedFailure>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await WriteFamilyStateAndIntentsAsync(session, context, rollbackTenantId, "physical", ct);
            await versions.IncrementPhysicalEntitlementVersionAsync(session, rollbackTenantId, ModuleCode, ct);
            throw new InjectedFailure();
        }));

        Assert.Equal(rollbackBaseline, await ReadVectorAsync(database, rollbackTenantId));
        Assert.Equal(0, await CountForTenantAsync(database, StateCollection, rollbackTenantId));
        Assert.Equal(0, await CountForTenantAsync(database, IntegrationOutbox, rollbackTenantId));
        Assert.Equal(0, await CountForTenantAsync(database, AuditOutbox, rollbackTenantId));

        Console.WriteLine("PHASE2A_VECTOR start=0/0/0 physical=1/0/0 subscription=1/1/0 global=1/1/1 noop=1/1/1 rollback=baseline");
    }

    [Fact]
    public async Task CrossFamilyConcurrentTransactions_IsolateCountersScopesAndIntentIdentities()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        await ProvisionCollectionsAsync(database);
        var context = new PlatformDbContext(mongo.Client, database);
        var executor = new PlatformTransactionExecutor(context);
        var versions = new EntitlementStateVersionRepository(context);
        var tenantId = Guid.NewGuid();

        await Task.WhenAll(
            ExecuteFamilyAsync(executor, versions, context, tenantId, "physical"),
            ExecuteFamilyAsync(executor, versions, context, tenantId, "subscription"),
            ExecuteFamilyAsync(executor, versions, context, tenantId, "global"));

        Assert.Equal(new VersionVector(1, 1, 1), await ReadVectorAsync(database, tenantId));
        var states = await database.GetCollection<BsonDocument>(StateCollection)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(3, states.Count);
        Assert.Equal(3, states.Select(x => x["Family"].AsString).Distinct(StringComparer.Ordinal).Count());
        Assert.All(states.Where(x => x["Family"].AsString != "global"),
            x => Assert.Equal(tenantId.ToString("D"), x["TenantId"].AsString));
        Assert.Equal("global:catalog-applicability",
            states.Single(x => x["Family"].AsString == "global")["Scope"].AsString);

        var integration = await database.GetCollection<BsonDocument>(IntegrationOutbox)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var audit = await database.GetCollection<BsonDocument>(AuditOutbox)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(3, integration.Count);
        Assert.Equal(3, audit.Count);
        Assert.Equal(3, integration.Select(x => x["EventId"].AsString).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, audit.Select(x => x["IdempotencyKey"].AsString).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, integration.Select(x => x["CorrelationId"].AsString).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(integration.Select(x => x["CorrelationId"].AsString).Order(),
            audit.Select(x => x["CorrelationId"].AsString).Order());

        Console.WriteLine($"PHASE2A_CROSS_FAMILY port={mongo.Port} vector=1/1/1 state=3 integrationOutbox=3 auditOutbox=3 uniqueEvents=3 uniqueCorrelations=3");
    }

    private static Task ExecuteFamilyAsync(PlatformTransactionExecutor executor,
        EntitlementStateVersionRepository versions, IPlatformDbContext context, Guid tenantId, string family) =>
        executor.ExecuteAsync(async (session, ct) =>
        {
            await WriteFamilyStateAndIntentsAsync(session, context, tenantId, family, ct);
            _ = family switch
            {
                "physical" => await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, ModuleCode, ct),
                "subscription" => await versions.IncrementSubscriptionSelectionVersionAsync(session, tenantId, ct),
                "global" => await versions.IncrementGlobalApplicabilityVersionAsync(session, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(family))
            };
            return 0;
        });

    private static async Task WriteFamilyStateAndIntentsAsync(IPlatformTransactionSession session,
        IPlatformDbContext context, Guid tenantId, string family, CancellationToken ct)
    {
        var handle = PlatformMongoTransactionSession.Require(session, context);
        var eventId = Guid.NewGuid().ToString("D");
        var correlationId = Guid.NewGuid().ToString("D");
        var tenant = tenantId.ToString("D");
        var scope = family == "global" ? "global:catalog-applicability" : $"tenant:{tenant}:module:{ModuleCode}";
        await context.Database.GetCollection<BsonDocument>(StateCollection).InsertOneAsync(handle,
            new BsonDocument { ["Family"] = family, ["TenantId"] = tenant, ["ModuleCode"] = ModuleCode, ["Scope"] = scope },
            cancellationToken: ct);
        await context.Database.GetCollection<BsonDocument>(IntegrationOutbox).InsertOneAsync(handle,
            new BsonDocument { ["EventId"] = eventId, ["CorrelationId"] = correlationId, ["TenantId"] = tenant, ["Family"] = family },
            cancellationToken: ct);
        await context.Database.GetCollection<BsonDocument>(AuditOutbox).InsertOneAsync(handle,
            new BsonDocument { ["IdempotencyKey"] = $"phase2a:{family}:{eventId}", ["CorrelationId"] = correlationId,
                ["TenantId"] = tenant, ["Family"] = family }, cancellationToken: ct);
    }

    private static async Task ProvisionCollectionsAsync(IMongoDatabase database)
    {
        foreach (var collection in new[] { StateCollection, IntegrationOutbox, AuditOutbox,
                     EntitlementStateVersionRepository.CollectionName })
            await database.CreateCollectionAsync(collection);
    }

    private static async Task<VersionVector> ReadVectorAsync(IMongoDatabase database, Guid tenantId)
    {
        var documents = await database.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        ulong Value(string key) => documents.Where(x => x["_id"].AsString == key)
            .Select(x => checked((ulong)x["Value"].ToInt64())).SingleOrDefault();
        return new VersionVector(
            Value($"physical:{tenantId:D}:{ModuleCode}"),
            Value($"subscription:{tenantId:D}"),
            Value("global:catalog-applicability"));
    }

    private static Task<long> CountAsync(IMongoDatabase database, string collection) =>
        database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    private static Task<long> CountForTenantAsync(IMongoDatabase database, string collection, Guid tenantId) =>
        database.GetCollection<BsonDocument>(collection)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", tenantId.ToString("D")));

    private sealed record VersionVector(ulong Physical, ulong Subscription, ulong Global);
    private sealed class InjectedFailure : Exception;
}
