using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class SubscriptionSessionAndStandaloneFailureTests
{
    [Fact]
    public async Task SubscriptionRepository_NullInactiveUnstartedWrongClientAndDisposedSessions_FailClosedWithZeroWrites()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var tenantId = Guid.NewGuid(); var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
        var repository = new TenantSubscriptionRepository(context, tenantContext);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.CreateAsync(null!, Subscription(tenantId), default));

        using (var raw = await mongo.Client.StartSessionAsync())
        {
            var unstarted = new PlatformMongoTransactionSession(mongo.Client, raw);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(unstarted, Subscription(tenantId), default));
        }
        using (var raw = await mongo.Client.StartSessionAsync())
        {
            raw.StartTransaction(); await raw.AbortTransactionAsync();
            var inactive = new PlatformMongoTransactionSession(mongo.Client, raw);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(inactive, Subscription(tenantId), default));
        }
        var otherClient = new MongoClient(mongo.ConnectionString);
        using (var raw = await otherClient.StartSessionAsync())
        {
            raw.StartTransaction();
            var wrong = new PlatformMongoTransactionSession(otherClient, raw);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(wrong, Subscription(tenantId), default));
            await raw.AbortTransactionAsync();
        }
        var disposedRaw = await mongo.Client.StartSessionAsync(); disposedRaw.StartTransaction();
        var disposed = new PlatformMongoTransactionSession(mongo.Client, disposedRaw); disposedRaw.Dispose();
        await Assert.ThrowsAnyAsync<Exception>(() => repository.CreateAsync(disposed, Subscription(tenantId), default));

        Assert.Equal(0, await database.GetCollection<BsonDocument>("tenant_subscriptions")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        foreach (var name in Participants.Where(x => x != "tenant_subscriptions"))
            Assert.Equal(0, await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task StandaloneMongo_ProductionSubscriptionWriter_IsTyped503AndAllParticipantsZero()
    {
        await using var mongo = await DisposableStandaloneMongo.StartAsync();
        Assert.True(mongo.Port >= 27022); Assert.NotEqual(27017, mongo.Port); Assert.NotEqual(27018, mongo.Port);
        var database = mongo.CreateDatabase(); var context = new PlatformDbContext(mongo.Client, database);
        var tenantId = Guid.NewGuid(); var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
        var subscriptions = new TenantSubscriptionRepository(context, tenantContext);
        var tenants = new TenantRegistryRepository(context, tenantContext);
        var plans = new SubscriptionPlanRepository(context, tenantContext);
        var tenant = new Tenant { Id = tenantId, Code = "SOLO", Slug = "solo", Name = "Solo", DisplayName = "Solo",
            Domain = "solo.local", Status = TenantStatus.Provisioning };
        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Code = "SOLO", Name = "Solo", IsActive = true };
        await database.GetCollection<Tenant>("tenants").InsertOneAsync(tenant);
        await database.GetCollection<SubscriptionPlan>("platform_subscription_plans").InsertOneAsync(plan);
        var user = new Mock<ICurrentUserContext>(); user.SetupGet(x => x.ActorName).Returns("standalone");
        var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(context), subscriptions, tenants, plans,
            new EntitlementStateVersionRepository(context), new IntentWriter(context), new AuditWriter(context), user.Object);
        var subscription = Subscription(tenantId); subscription.PlanId = plan.Id;

        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => writer.CreateAsync(subscription,
            tenant, plan, "StandaloneEvidence", AuditOperation.Create, async (session, current, _, ct) =>
            {
                var handle = PlatformMongoTransactionSession.Require(session, context);
                await database.GetCollection<BsonDocument>("quota_usages").InsertOneAsync(handle, new("value", 1), cancellationToken: ct);
                await database.GetCollection<BsonDocument>("quota_events").InsertOneAsync(handle, new("value", 1), cancellationToken: ct);
                return Response<NoContent>.Success(204);
            }, default));
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(1, await database.GetCollection<BsonDocument>("tenants").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        foreach (var name in Participants)
            Assert.Equal(0, await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    private static readonly string[] Participants = ["tenant_subscriptions", EntitlementStateVersionRepository.CollectionName,
        "quota_usages", "quota_events", "outbox_events", "audit_outbox"];
    private static TenantSubscription Subscription(Guid tenantId) => new() { Id = Guid.NewGuid(), TenantId = tenantId,
        PlanId = Guid.NewGuid(), Status = TenantSubscriptionStatus.Active, RowVersion = Guid.NewGuid().ToByteArray() };
    private sealed class IntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    { public async Task<EventEnvelope<T>> EnqueueAsync<T>(IPlatformTransactionSession session, T value, EventPublishOptions options,
          CancellationToken cancellationToken = default) where T : IIntegrationEvent
      { await context.Database.GetCollection<BsonDocument>("outbox_events").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
            new("value", 1), cancellationToken: cancellationToken); return new(new(options.EventId!.Value, value.EventName,
            value.EventVersion, options.CorrelationId!.Value, null, options.TenantId, options.Producer!, options.OccurredAtUtc!.Value), value); } }
    private sealed class AuditWriter(IPlatformDbContext context) : ITransactionalAuditOutboxWriter
    { public async Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default)
      { await context.Database.GetCollection<BsonDocument>("audit_outbox").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
            new("value", 1), cancellationToken: ct); return true; } }
}
