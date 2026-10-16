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
            repository.TryInsertAsync(session, Request(), ct)));

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
            await repository.TryInsertAsync(session, Request(), ct);
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
            (session, ct) => repository.TryInsertAsync(session, Request(), ct)));

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    /// <summary>
    /// WP-PLATFORM-AUDIT-INTX-01 — the store lets no row in that the outbox mapper could not deliver: the shape every
    /// in-transaction caller wrote before the fix ({ Outcome, ModuleCode } and the like) now FAILS THE TRANSACTION
    /// instead of sitting in dead letter beside a committed business change.
    /// </summary>
    [Theory]
    [InlineData("TenantId")]
    [InlineData("ActorType")]
    [InlineData("Category")]
    [InlineData("SourceService")]
    public async Task A_payload_the_mapper_could_not_deliver_is_refused_and_nothing_is_written(string missing)
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var repository = new AuditOutboxRepository(context);
        var executor = new PlatformTransactionExecutor(context);
        var canonical = Request();
        var broken = new AuditOutboxWriteRequest
        {
            TenantId = canonical.TenantId, CorrelationId = canonical.CorrelationId, IdempotencyKey = canonical.IdempotencyKey,
            RequestType = canonical.RequestType, Operation = canonical.Operation, EntityType = canonical.EntityType, EntityId = canonical.EntityId,
            Payload = canonical.Payload.Where(pair => pair.Key != missing).ToDictionary(pair => pair.Key, pair => pair.Value)
        };

        var refusal = await Assert.ThrowsAsync<Diten.Platform.Infrastructure.Services.Audit.AuditOutboxPayloadMappingException>(
            () => executor.ExecuteAsync((session, ct) => repository.TryInsertAsync(session, broken, ct)));

        Assert.Contains($"Field={missing}", refusal.Message);
        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    [Fact]
    public async Task The_pre_fix_hand_written_payload_is_refused()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var context = new PlatformDbContext(replicaSet.Client, database);
        var canonical = Request();
        var preFix = new AuditOutboxWriteRequest
        {
            TenantId = canonical.TenantId, CorrelationId = canonical.CorrelationId, IdempotencyKey = canonical.IdempotencyKey,
            RequestType = canonical.RequestType, Operation = canonical.Operation, EntityType = canonical.EntityType, EntityId = canonical.EntityId,
            Payload = new Dictionary<string, object?> { ["ModuleCode"] = "CRM", ["Outcome"] = "Succeeded" }
        };

        await Assert.ThrowsAsync<Diten.Platform.Infrastructure.Services.Audit.AuditOutboxPayloadMappingException>(
            () => new PlatformTransactionExecutor(context).ExecuteAsync((session, ct) => new AuditOutboxRepository(context).TryInsertAsync(session, preFix, ct)));

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<AuditOutboxMessage>.Empty));
    }

    // The canonical payload, from the one builder — what the store is handed in production.
    private static AuditOutboxWriteRequest Request()
    {
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        return new AuditOutboxWriteRequest
        {
            TenantId = tenantId,
            CorrelationId = correlationId,
            IdempotencyKey = "physical:" + Guid.NewGuid().ToString("N"),
            RequestType = "AddTenantModuleEntitlementCommand",
            Operation = AuditOperation.Assign,
            EntityType = "TenantModuleEntitlement",
            EntityId = entityId,
            Payload = Diten.Platform.Application.Features.Audit.AuditOutboxPayload.Build(
                new Diten.Platform.Application.Features.Audit.AuditCanonicalRecord(
                    tenantId, correlationId, "AddTenantModuleEntitlementCommand", AuditActorType.PlatformAdministrator, Guid.NewGuid(),
                    "platform.admin@di10.test", "Platform Admin", tenantId, AuditCategory.SubscriptionBilling, "TenantModuleEntitlement", entityId,
                    AuditOperation.Assign, AuditOutcome.Succeeded, null, null, null, null, null, null, "Diten.Platform", "subscription-billing", false),
                new Diten.Platform.Application.Features.Audit.SensitiveFieldRedactor(new Diten.Platform.Application.Features.Audit.SensitiveFieldRedactionRegistry()))
        };
    }

    private static IMongoCollection<AuditOutboxMessage> Collection(IMongoDatabase database) =>
        database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);

    private sealed class InjectedFailure : Exception;
}
