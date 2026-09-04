using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TransactionalAuditOutboxMongoTests
{
    [Fact]
    public async Task TransactionalEnqueue_CommitsWithTransaction()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = new AuditOutboxRepository(context);
        var executor = new PlatformTransactionExecutor(context);

        Assert.True(await executor.ExecuteAsync((session, ct) =>
            repository.TryEnqueueAsync(session, Request(), ct)));

        Assert.Equal(1, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    [Fact]
    public async Task FaultAfterTransactionalEnqueue_LeavesNoAuditResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = new AuditOutboxRepository(context);
        var executor = new PlatformTransactionExecutor(context);

        await Assert.ThrowsAsync<InjectedFailure>(() => executor.ExecuteAsync<int>(async (session, ct) =>
        {
            await repository.TryEnqueueAsync(session, Request(), ct);
            throw new InjectedFailure();
        }));

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    [Fact]
    public async Task DifferentClientSession_IsRejectedBeforeAuditWrite()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var ownerContext = new PlatformDbContext(replicaSet.Client, database);
        var otherContext = new PlatformDbContext(new MongoClient(replicaSet.ConnectionString), database);
        var repository = new AuditOutboxRepository(otherContext);
        var executor = new PlatformTransactionExecutor(ownerContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            (session, ct) => repository.TryEnqueueAsync(session, Request(), ct)));

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    private static AuditOutboxWriteRequest Request() => new()
    {
        TenantId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = "physical:" + Guid.NewGuid().ToString("N"),
        RequestType = "AddTenantModuleEntitlementCommand",
        Operation = AuditOperation.Assign,
        EntityType = "TenantModuleEntitlement",
        EntityId = Guid.NewGuid(),
        Payload = new Dictionary<string, object?> { ["outcome"] = "succeeded" }
    };

    private static IMongoCollection<AuditOutboxMessage> Collection(IMongoDatabase database) =>
        database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);

    private sealed class InjectedFailure : Exception;
}
