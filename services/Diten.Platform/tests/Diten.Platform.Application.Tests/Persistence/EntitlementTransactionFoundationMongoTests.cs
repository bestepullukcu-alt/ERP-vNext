using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class EntitlementTransactionFoundationMongoTests
{
    [Fact]
    public async Task PhysicalMutation_AndCounter_CommitAtomically()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var tenantContext = TenantContextFor(Guid.NewGuid());
        var repository = new TenantModuleEntitlementRepository(context, tenantContext);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var tenantId = tenantContext.TenantId;

        var returnedVersion = await executor.ExecuteAsync(async (session, ct) =>
        {
            await repository.CreateAsync(session, Entitlement(tenantId, "PPM"), ct);
            return await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct);
        });

        Assert.Equal(1UL, returnedVersion);
        Assert.Equal(1, await database.GetCollection<BsonDocument>("tenant_module_entitlements")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1, await database.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task FaultAfterCounter_RollsBackBusinessAndCounterWithZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var tenantContext = TenantContextFor(Guid.NewGuid());
        var repository = new TenantModuleEntitlementRepository(context, tenantContext);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var tenantId = tenantContext.TenantId;

        await Assert.ThrowsAsync<InjectedFailure>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await repository.CreateAsync(session, Entitlement(tenantId, "PPM"), ct);
            await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct);
            throw new InjectedFailure();
        }));

        Assert.Equal(0, await database.GetCollection<BsonDocument>("tenant_module_entitlements")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task ConcurrentCounterMutations_DoNotLoseIncrement()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var tenantId = Guid.NewGuid();

        // MongoDB cannot create the same collection concurrently from separate
        // transactions. Provision the test collection before measuring counter contention.
        await database.CreateCollectionAsync(EntitlementStateVersionRepository.CollectionName);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            executor.ExecuteAsync((session, ct) =>
                versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct))));

        Assert.Equal(Enumerable.Range(1, 2).Select(x => (ulong)x), results.Order());
    }

    [Fact]
    public async Task SessionFromSecondClient_IsRejectedBeforeMutation()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var firstContext = new PlatformDbContext(replicaSet.Client, database);
        var secondClient = new MongoClient(replicaSet.ConnectionString);
        var secondContext = new PlatformDbContext(secondClient, database);
        var versions = new EntitlementStateVersionRepository(secondContext);
        var executor = new PlatformTransactionExecutor(firstContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            (session, ct) => versions.IncrementGlobalApplicabilityVersionAsync(session, ct)));
    }

    private static TenantContext TenantContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private static TenantModuleEntitlement Entitlement(Guid tenantId, string moduleCode) => new()
    {
        TenantId = tenantId,
        ModuleCode = moduleCode,
        Source = EntitlementSource.Addon,
        IsEnabled = true
    };

    private sealed class InjectedFailure : Exception;
}
