using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PhysicalEntitlementTransactionGuardMongoTests
{
    [Theory]
    [InlineData("Add/Create")]
    [InlineData("Enable")]
    [InlineData("Disable")]
    [InlineData("UpdateExpiry")]
    [InlineData("RemoveOverride/SoftDelete")]
    public async Task ExactPhysicalSessionlessSeam_FailsBeforeMongoWrite_WithAllResidueZero(string command)
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var writeCommands = 0;
        var settings = MongoClientSettings.FromConnectionString(replicaSet.ConnectionString);
        settings.ClusterConfigurator = cluster => cluster.Subscribe<CommandStartedEvent>(started =>
        {
            if (started.CommandName is "insert" or "update" or "delete" or "findAndModify")
                Interlocked.Increment(ref writeCommands);
        });
        var observedClient = new MongoClient(settings);
        var observedDatabase = observedClient.GetDatabase(database.DatabaseNamespace.DatabaseName);
        var repository = Repository(observedClient, observedDatabase, tenantId);
        var entitlement = Entitlement(tenantId, "PPM");

#pragma warning disable CS0618
        var error = command switch
        {
            "Add/Create" => await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => repository.CreateAsync(entitlement)),
            "RemoveOverride/SoftDelete" => await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => repository.SoftDeleteAsync(tenantId, entitlement.Id, entitlement.RowVersion)),
            _ => await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => repository.UpdateAsync(entitlement, entitlement.RowVersion))
        };
#pragma warning restore CS0618

        Assert.Equal(503, error.StatusCode);
        Assert.Equal(0, Volatile.Read(ref writeCommands));
        await AssertAllParticipantResidueZero(database);
    }

    [Fact]
    public async Task InvalidSessionOwnershipVariants_FailClosedBeforeWrite_WithZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = Repository(context, tenantId);
        var entitlement = Entitlement(tenantId, "PPM");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(null!, entitlement));

        using var inactiveHandle = await replicaSet.Client.StartSessionAsync();
        inactiveHandle.StartTransaction();
        await inactiveHandle.AbortTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(
            new PlatformMongoTransactionSession(replicaSet.Client, inactiveHandle), entitlement));

        using var notStartedHandle = await replicaSet.Client.StartSessionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(
            new PlatformMongoTransactionSession(replicaSet.Client, notStartedHandle), entitlement));

        var otherClient = new MongoClient(replicaSet.ConnectionString);
        using var otherHandle = await otherClient.StartSessionAsync();
        otherHandle.StartTransaction();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(
            new PlatformMongoTransactionSession(otherClient, otherHandle), entitlement));
        await otherHandle.AbortTransactionAsync();

        await AssertAllParticipantResidueZero(database);
    }

    [Fact]
    public async Task StaleCas_RollsBackCounterAndPreservesCommittedBusinessState()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = Repository(context, tenantId);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var entitlement = Entitlement(tenantId, "PPM");

        await executor.ExecuteAsync(async (session, ct) =>
        {
            await repository.CreateAsync(session, entitlement, ct);
            return await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct);
        });

        var staleRowVersion = Guid.NewGuid().ToByteArray();
        entitlement.IsEnabled = false;
        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(() =>
            executor.ExecuteAsync(async (session, ct) =>
            {
                await repository.UpdateAsync(session, entitlement, staleRowVersion, ct);
                return await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct);
            }));

        var stored = await database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements")
            .Find(x => x.Id == entitlement.Id).SingleAsync();
        Assert.True(stored.IsEnabled);
        Assert.Equal(1L, await PhysicalVersionAsync(database, tenantId, "PPM"));
    }

    [Fact]
    public async Task CrossTenantUpdate_IsNonDisclosingAndLeavesStateAndCounterUntouched()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var ownerTenant = Guid.NewGuid();
        var attackerTenant = Guid.NewGuid();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var ownerRepository = Repository(context, ownerTenant);
        var attackerRepository = Repository(context, attackerTenant);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var entitlement = Entitlement(ownerTenant, "PPM");

        await executor.ExecuteAsync(async (session, ct) =>
        {
            await ownerRepository.CreateAsync(session, entitlement, ct);
            return await versions.IncrementPhysicalEntitlementVersionAsync(session, ownerTenant, "PPM", ct);
        });

        entitlement.IsEnabled = false;
        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(() =>
            executor.ExecuteAsync(async (session, ct) =>
            {
                await attackerRepository.UpdateAsync(session, entitlement, entitlement.RowVersion, ct);
                return await versions.IncrementPhysicalEntitlementVersionAsync(session, attackerTenant, "PPM", ct);
            }));

        var stored = await database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements")
            .Find(x => x.Id == entitlement.Id).SingleAsync();
        Assert.True(stored.IsEnabled);
        Assert.Equal(1L, await PhysicalVersionAsync(database, ownerTenant, "PPM"));
        Assert.Equal(0L, await PhysicalVersionAsync(database, attackerTenant, "PPM"));
    }

    [Fact]
    public async Task CancellationInsideBody_AbortsBusinessAndCounterWithZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = Repository(context, tenantId);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await repository.CreateAsync(session, Entitlement(tenantId, "PPM"), ct);
            await versions.IncrementPhysicalEntitlementVersionAsync(session, tenantId, "PPM", ct);
            throw new OperationCanceledException("Injected cancellation before commit.");
        }));

        Assert.Equal(0, await CountAsync(database, "tenant_module_entitlements"));
        Assert.Equal(0, await CountAsync(database, EntitlementStateVersionRepository.CollectionName));
    }

    [Fact]
    public async Task PhysicalCounter_IsScopedByTenantAndCanonicalModule()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var versions = new EntitlementStateVersionRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();

        Assert.Equal(1UL, await executor.ExecuteAsync((session, ct) =>
            versions.IncrementPhysicalEntitlementVersionAsync(session, firstTenant, "PPM", ct)));
        Assert.Equal(2UL, await executor.ExecuteAsync((session, ct) =>
            versions.IncrementPhysicalEntitlementVersionAsync(session, firstTenant, "ppm", ct)));
        Assert.Equal(1UL, await executor.ExecuteAsync((session, ct) =>
            versions.IncrementPhysicalEntitlementVersionAsync(session, firstTenant, "MDM", ct)));
        Assert.Equal(1UL, await executor.ExecuteAsync((session, ct) =>
            versions.IncrementPhysicalEntitlementVersionAsync(session, secondTenant, "PPM", ct)));
    }

    private static TenantModuleEntitlementRepository Repository(
        IMongoClient client,
        IMongoDatabase database,
        Guid tenantId) => Repository(new PlatformDbContext(client, database), tenantId);

    private static TenantModuleEntitlementRepository Repository(IPlatformDbContext context, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return new TenantModuleEntitlementRepository(context, tenantContext);
    }

    private static TenantModuleEntitlement Entitlement(Guid tenantId, string moduleCode) => new()
    {
        TenantId = tenantId,
        ModuleCode = moduleCode,
        Source = EntitlementSource.Addon,
        IsEnabled = true
    };

    private static Task<long> CountAsync(IMongoDatabase database, string collectionName) =>
        database.GetCollection<BsonDocument>(collectionName)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    private static async Task AssertAllParticipantResidueZero(IMongoDatabase database)
    {
        foreach (var collection in new[]
                 {
                     "tenant_module_entitlements", "quota_usages", "quota_events",
                     EntitlementStateVersionRepository.CollectionName, "outbox_events", "audit_outbox"
                 })
            Assert.Equal(0, await CountAsync(database, collection));
    }

    private static async Task<long> PhysicalVersionAsync(
        IMongoDatabase database,
        Guid tenantId,
        string moduleCode)
    {
        var id = $"physical:{tenantId:D}:{moduleCode.ToUpperInvariant()}";
        var document = await database
            .GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync();
        return document?["Value"].ToInt64() ?? 0L;
    }
}
