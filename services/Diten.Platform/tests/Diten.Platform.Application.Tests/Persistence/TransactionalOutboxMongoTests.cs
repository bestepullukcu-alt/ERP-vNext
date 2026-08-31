using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TransactionalOutboxMongoTests
{
    [Fact]
    public async Task Enqueue_CommitsExactlyOneIntentWithBusinessTransaction()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        await EnsureUniqueEventIndexAsync(database);
        var context = new PlatformDbContext(replicaSet.Client, database);
        var tenantId = Guid.NewGuid();
        var repository = new OutboxEventRepository(context, TenantContextFor(tenantId));
        var executor = new PlatformTransactionExecutor(context);

        var result = await executor.ExecuteAsync((session, ct) =>
            repository.EnqueueAsync(session, CreateRequest(tenantId), ct));

        Assert.Equal(EventOutboxWriteResult.Inserted, result);
        Assert.Equal(1, await database.GetCollection<OutboxEvent>("outbox_events")
            .CountDocumentsAsync(FilterDefinition<OutboxEvent>.Empty));
    }

    [Fact]
    public async Task FaultAfterEnqueue_RollsBackIntentWithZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        await EnsureUniqueEventIndexAsync(database);
        var context = new PlatformDbContext(replicaSet.Client, database);
        var tenantId = Guid.NewGuid();
        var repository = new OutboxEventRepository(context, TenantContextFor(tenantId));
        var executor = new PlatformTransactionExecutor(context);

        await Assert.ThrowsAsync<InjectedFailure>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await repository.EnqueueAsync(session, CreateRequest(tenantId), ct);
            throw new InjectedFailure();
        }));

        Assert.Equal(0, await database.GetCollection<OutboxEvent>("outbox_events")
            .CountDocumentsAsync(FilterDefinition<OutboxEvent>.Empty));
    }

    [Fact]
    public async Task DuplicateIntent_FailsTransactionAndLeavesZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        await EnsureUniqueEventIndexAsync(database);
        var context = new PlatformDbContext(replicaSet.Client, database);
        var tenantId = Guid.NewGuid();
        var repository = new OutboxEventRepository(context, TenantContextFor(tenantId));
        var executor = new PlatformTransactionExecutor(context);
        var request = CreateRequest(tenantId);

        var exception = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await repository.EnqueueAsync(session, request, ct);
            await repository.EnqueueAsync(session, request, ct);
            return 0;
        }));
        Assert.IsType<MongoWriteException>(exception.InnerException);

        Assert.Equal(0, await database.GetCollection<OutboxEvent>("outbox_events")
            .CountDocumentsAsync(FilterDefinition<OutboxEvent>.Empty));
    }

    [Fact]
    public async Task SessionOwnedByDifferentMongoClient_IsRejectedBeforeEnqueue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var ownerContext = new PlatformDbContext(replicaSet.Client, database);
        var otherContext = new PlatformDbContext(new MongoClient(replicaSet.ConnectionString), database);
        var tenantId = Guid.NewGuid();
        var repository = new OutboxEventRepository(otherContext, TenantContextFor(tenantId));
        var executor = new PlatformTransactionExecutor(ownerContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            (session, ct) => repository.EnqueueAsync(session, CreateRequest(tenantId), ct)));

        Assert.Equal(0, await database.GetCollection<OutboxEvent>("outbox_events")
            .CountDocumentsAsync(FilterDefinition<OutboxEvent>.Empty));
    }

    private static EventOutboxWriteRequest CreateRequest(Guid tenantId)
    {
        var metadata = new EventMetadata(
            Guid.NewGuid(),
            "platform.entitlement.changed.v1",
            1,
            Guid.NewGuid(),
            null,
            tenantId,
            "Diten.Platform",
            DateTimeOffset.UtcNow);
        return new EventOutboxWriteRequest(
            metadata,
            Encoding.UTF8.GetBytes("{\"kind\":\"physical\"}"),
            TrustedTransportMetadata.Empty);
    }

    private static TenantContext TenantContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private static Task EnsureUniqueEventIndexAsync(IMongoDatabase database) =>
        database.GetCollection<OutboxEvent>("outbox_events").Indexes.CreateOneAsync(
            new CreateIndexModel<OutboxEvent>(
                Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventId),
                new CreateIndexOptions { Unique = true, Name = "ux_outbox_events_event_id" }));

    private sealed class InjectedFailure : Exception;
}
