using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Common.Tenancy;
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
public sealed class SubscriptionTransactionWriterMongoTests
{
    public static TheoryData<string> SuccessPaths => new() { "Create", "Assign", "Activate", "Cancel", "Expire", "Reactivate", "Renew", "Suspend" };

    [Theory]
    [MemberData(nameof(SuccessPaths))]
    public async Task ExactEightSuccessPaths_CommitEveryApplicableParticipantExactOnce(string path)
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var context = new PlatformDbContext(mongo.Client, database);
        var tenantContext = TenantContextFor(tenantId);
        var subscriptions = new TenantSubscriptionRepository(context, tenantContext);
        var tenants = new TenantRegistryRepository(context, tenantContext);
        var plans = new Mock<ISubscriptionPlanRepository>();
        var plan = new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true,
            DefaultQuotas = new Dictionary<string, decimal> { ["users.max"] = 10 } };
        plans.Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.ActorName).Returns("evidence-operator");
        var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(context), subscriptions,
            tenants, plans.Object, new EntitlementStateVersionRepository(context),
            new MongoIntentWriter(context), new MongoAuditWriter(context), currentUser.Object);
        var tenant = Tenant(tenantId);
        await database.GetCollection<Tenant>("tenants").InsertOneAsync(tenant);
        var subscription = Subscription(subscriptionId, tenantId, planId, InitialStatus(path));

        Response<NoContent>? update = null;
        Response<Guid>? create = null;
        if (path is "Create" or "Assign")
        {
            create = await writer.CreateAsync(subscription, tenant, plan, path, AuditOperation.Create,
                UsesQuota(path) ? QuotaParticipant(database, context) : null, CancellationToken.None);
            Assert.True(create.IsSuccessful);
        }
        else
        {
            await database.GetCollection<TenantSubscription>("tenant_subscriptions").InsertOneAsync(subscription);
            var expected = subscription.RowVersion;
            ApplyMutation(path, subscription);
            update = await writer.UpdateAsync(subscription, expected, planId, InitialStatus(path).ToString(), path,
                AuditOperation.LifecycleTransition, path == "Activate", UsesQuota(path) ? QuotaParticipant(database, context) : null,
                CancellationToken.None);
            Assert.True(update.IsSuccessful);
        }

        Assert.Equal(1, await Count(database, "tenant_subscriptions"));
        Assert.Equal(1, await Count(database, "tenants"));
        Assert.Equal(1, await Count(database, EntitlementStateVersionRepository.CollectionName));
        Assert.Equal(UsesQuota(path) ? 1 : 0, await Count(database, "quota_usages"));
        Assert.Equal(UsesQuota(path) ? 1 : 0, await Count(database, "quota_events"));
        Assert.Equal(1, await Count(database, "outbox_events"));
        Assert.Equal(1, await Count(database, "audit_outbox"));
        var savedTenant = await database.GetCollection<Tenant>("tenants").Find(x => x.Id == tenantId).SingleAsync();
        Assert.Equal(subscription.Status, savedTenant.SubscriptionStatus);
    }

    [Fact]
    public async Task ExactNoOp_LeavesEveryParticipantUnchanged()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        foreach (var collection in ParticipantCollections)
            Assert.Equal(0, await Count(database, collection));
    }

    private static readonly string[] ParticipantCollections = ["tenant_subscriptions", "tenants",
        EntitlementStateVersionRepository.CollectionName, "quota_usages", "quota_events", "outbox_events", "audit_outbox"];

    private static Func<IPlatformTransactionSession, TenantSubscription, SubscriptionPlan, CancellationToken, Task<Response<NoContent>>> QuotaParticipant(
        IMongoDatabase database, IPlatformDbContext context) => async (session, subscription, _, ct) =>
    {
        var handle = PlatformMongoTransactionSession.Require(session, context);
        await database.GetCollection<BsonDocument>("quota_usages").InsertOneAsync(handle,
            new BsonDocument { ["TenantId"] = subscription.TenantId.ToString(), ["SubscriptionId"] = subscription.Id.ToString() }, cancellationToken: ct);
        await database.GetCollection<BsonDocument>("quota_events").InsertOneAsync(handle,
            new BsonDocument { ["TenantId"] = subscription.TenantId.ToString(), ["SubscriptionId"] = subscription.Id.ToString() }, cancellationToken: ct);
        return Response<NoContent>.Success(204);
    };

    private static bool UsesQuota(string path) => path is "Assign" or "Activate";
    private static TenantSubscriptionStatus InitialStatus(string path) => path switch
    {
        "Activate" => TenantSubscriptionStatus.PendingProvisioning,
        "Reactivate" => TenantSubscriptionStatus.Suspended,
        _ => TenantSubscriptionStatus.Active
    };
    private static void ApplyMutation(string path, TenantSubscription subscription)
    {
        subscription.Status = path switch
        {
            "Cancel" => TenantSubscriptionStatus.Cancelled,
            "Expire" => TenantSubscriptionStatus.Expired,
            "Reactivate" or "Activate" or "Renew" => TenantSubscriptionStatus.Active,
            "Suspend" => TenantSubscriptionStatus.Suspended,
            _ => subscription.Status
        };
        subscription.UpdatedBy = "evidence-operator";
    }
    private static Tenant Tenant(Guid id) => new() { Id = id, Code = "EVID", Slug = "evid",
        Name = "Evidence", DisplayName = "Evidence", Domain = "evidence.local", Status = TenantStatus.Provisioning };
    private static TenantSubscription Subscription(Guid id, Guid tenantId, Guid planId, TenantSubscriptionStatus status) => new()
    { Id = id, TenantId = tenantId, PlanId = planId, Status = status, RowVersion = Guid.NewGuid().ToByteArray(), UpdatedBy = "evidence-operator" };
    private static TenantContext TenantContextFor(Guid id) { var value = new TenantContext(); value.SetTenant(id); return value; }
    private static Task<long> Count(IMongoDatabase database, string collection) =>
        database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    private sealed class MongoIntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    {
        public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
        {
            var metadata = new EventMetadata(options.EventId!.Value, @event.EventName, @event.EventVersion,
                options.CorrelationId!.Value, null, options.TenantId, options.Producer!, options.OccurredAtUtc!.Value);
            await context.Database.GetCollection<BsonDocument>("outbox_events").InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument { ["EventId"] = metadata.EventId.ToString(), ["TenantId"] = options.TenantId!.Value.ToString() },
                cancellationToken: cancellationToken);
            return new EventEnvelope<TEvent>(metadata, @event);
        }
    }
    private sealed class MongoAuditWriter(IPlatformDbContext context) : ITransactionalAuditOutboxWriter
    {
        public async Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default)
        {
            await context.Database.GetCollection<BsonDocument>("audit_outbox").InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument { ["IdempotencyKey"] = request.IdempotencyKey, ["TenantId"] = request.TenantId.ToString() }, cancellationToken: ct);
            return true;
        }
    }
}
