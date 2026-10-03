using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — end to end on a real replica set, for the two in-transaction callers that are not the
/// entitlement screen (that one is driven over HTTP in <c>TenantModulesScreenHttpMongoTests</c>): the tenant
/// subscription writer and the global-applicability coordinator. Production writer, production store, production
/// <see cref="AuditOutboxProcessor"/>: a command's change must arrive in <c>audit_events</c> as ONE row that names the
/// actor, the category, the entity, the operation and the before/after state; processing twice must not double it; a
/// person's call with nobody to name must be refused and leave the business data untouched.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TransactionOwnedAuditDeliveryMongoTests
{
    private static readonly Guid Administrator = Guid.Parse("7b000000-0000-4000-8000-0000000000ad");

    // ── caller 2: tenant subscriptions ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suspending_a_subscription_reaches_audit_events_once_with_the_actor_and_the_status_before_and_after()
    {
        await using var world = await World.StartAsync();
        var (writer, subscription, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Active);
        var expected = subscription.RowVersion;
        subscription.Status = TenantSubscriptionStatus.Suspended;
        subscription.SuspendedAtUtc = DateTimeOffset.UtcNow;

        var response = await writer.UpdateAsync(subscription, expected, planId, "Active", "SuspendTenantSubscriptionCommand",
            AuditOperation.LifecycleTransition, false, null, CancellationToken.None);
        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors ?? []));

        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal("SuspendTenantSubscriptionCommand", audit.RequestType);
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal(Administrator, audit.ActorId);
        Assert.Equal("p***@di10.test", audit.ActorEmailMasked);
        Assert.Equal(AuditCategory.SubscriptionBilling, audit.Category);
        Assert.Equal("TenantSubscription", audit.EntityType);
        Assert.Equal(subscription.Id, audit.EntityId);
        Assert.Equal(AuditOperation.LifecycleTransition, audit.Operation);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(subscription.TenantId, audit.TenantId);
        Assert.Equal(subscription.TenantId, audit.TargetTenantId);
        Assert.Equal("Diten.Platform", audit.SourceService);
        Assert.Equal("Active", audit.BeforeState!["Status"]);
        Assert.Equal("Suspended", audit.AfterState!["Status"]);
        Assert.Equal(planId.ToString("D"), audit.AfterState["PlanId"]);
        Assert.StartsWith("tenant-subscription:", (string)audit.Metadata[AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey]!);
        Assert.Equal(0, await world.DeadLettersAsync());

        // Processing again finds nothing to do and writes no second row.
        Assert.Equal(0, await world.ProcessAsync());
        Assert.Single(await world.AuditEventsAsync());
    }

    [Fact]
    public async Task Renewing_a_subscription_records_the_period_end_before_and_after()
    {
        await using var world = await World.StartAsync();
        var (writer, subscription, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Active,
            periodEnd: new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var expected = subscription.RowVersion;
        subscription.CurrentPeriodEndUtc = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);

        Assert.True((await writer.UpdateAsync(subscription, expected, planId, "Active", "RenewTenantSubscriptionCommand",
            AuditOperation.LifecycleTransition, false, null, CancellationToken.None)).IsSuccessful);
        await world.ProcessAsync();

        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal("2026-12-31T00:00:00.0000000+00:00", audit.BeforeState!["CurrentPeriodEndUtc"]);
        Assert.Equal("2027-12-31T00:00:00.0000000+00:00", audit.AfterState!["CurrentPeriodEndUtc"]);
    }

    [Fact]
    public async Task A_subscription_change_with_nobody_to_name_is_refused_and_the_subscription_is_unchanged()
    {
        await using var world = await World.StartAsync();
        var (writer, subscription, planId) = await world.SubscriptionAsync(Nobody(), TenantSubscriptionStatus.Active);
        var expected = subscription.RowVersion;
        subscription.Status = TenantSubscriptionStatus.Suspended;

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() => writer.UpdateAsync(subscription, expected, planId,
            "Active", "SuspendTenantSubscriptionCommand", AuditOperation.LifecycleTransition, false, null, CancellationToken.None));

        var stored = await world.Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.Id == subscription.Id).SingleAsync();
        Assert.Equal(TenantSubscriptionStatus.Active, stored.Status); // same transaction: the business write rolled back
        Assert.Equal(expected, stored.RowVersion);
        Assert.Equal(0, await world.CountAsync("audit_outbox"));
        Assert.Equal(0, await world.CountAsync("outbox_events"));
        Assert.Equal(0, await world.CountAsync(EntitlementStateVersionRepository.CollectionName));
    }

    // ── caller 3: global applicability (module catalogue, subscription plans, manifest registration) ────

    [Fact]
    public async Task A_catalogue_change_by_a_platform_administrator_reaches_audit_events_once_as_a_platform_global_record()
    {
        await using var world = await World.StartAsync();
        var coordinator = world.Coordinator(Person("platform_admin"));
        var item = Guid.NewGuid();

        await coordinator.ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("DeactivateModuleCatalogItemCommand", AuditOperation.Deactivate, "ModuleCatalogItem", item),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));

        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal(Administrator, audit.ActorId);
        Assert.Equal(AuditCategory.ModuleCatalog, audit.Category);
        Assert.Equal("ModuleCatalogItem", audit.EntityType);
        Assert.Equal(item, audit.EntityId);
        Assert.Equal(AuditOperation.Deactivate, audit.Operation);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, audit.TenantId);
        Assert.Null(audit.TargetTenantId);
        Assert.Equal("module-catalog", audit.SourceModule);
        Assert.True(audit.Metadata.ContainsKey("GlobalApplicabilityVersion"));
        Assert.Equal(0, await world.DeadLettersAsync());

        Assert.Equal(0, await world.ProcessAsync());
        Assert.Single(await world.AuditEventsAsync());
    }

    [Theory]
    [InlineData("RegisterModuleManifestCommand", "ModuleCatalogItem", "module-self-registration:Diten.CrmService", AuditCategory.ModuleCatalog)]
    [InlineData("SeedDefaultSubscriptionPlansCommand", "SubscriptionPlan", "subscription-plan-startup-seed", AuditCategory.SubscriptionBilling)]
    public async Task An_unattended_job_is_recorded_as_the_system_actor_with_its_name(string requestType, string entityType, string job, AuditCategory category)
    {
        await using var world = await World.StartAsync();

        await world.Coordinator(Nobody()).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor(requestType, AuditOperation.Update, entityType, Guid.NewGuid(), SystemActor: job),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));

        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal(AuditActorType.System, audit.ActorType);
        Assert.Null(audit.ActorId);
        Assert.Equal(job, audit.Metadata[CanonicalTransactionalAuditOutboxWriter.SystemActorMetadataKey]);
        Assert.Equal(category, audit.Category);
        Assert.Equal(0, await world.DeadLettersAsync());
    }

    [Fact]
    public async Task A_catalogue_change_with_nobody_to_name_is_refused_and_the_global_version_does_not_move()
    {
        await using var world = await World.StartAsync();
        var projectionWritten = false;

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() => world.Coordinator(Nobody()).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("DeleteModuleCatalogItemCommand", AuditOperation.Delete, "ModuleCatalogItem", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => { projectionWritten = true; return Task.CompletedTask; }))));

        Assert.True(projectionWritten); // the business write ran inside the transaction …
        Assert.Equal(0, await world.CountAsync(EntitlementStateVersionRepository.CollectionName)); // … and rolled back with the refusal
        Assert.Equal(0, await world.CountAsync("audit_outbox"));
        Assert.Equal(0, await world.CountAsync("outbox_events"));
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────

    private static (ITenantAuthorizationContext, ICurrentUserContext) Person(string actorTypeClaim)
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(x => x.IsAuthenticated).Returns(true);
        principal.SetupGet(x => x.ActorType).Returns(actorTypeClaim);
        principal.SetupGet(x => x.UserId).Returns(Administrator);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(Administrator);
        user.SetupGet(x => x.Email).Returns("platform.admin@di10.test");
        user.SetupGet(x => x.DisplayName).Returns("Platform Admin");
        user.SetupGet(x => x.ActorName).Returns("platform.admin@di10.test");
        return (principal.Object, user.Object);
    }

    private static (ITenantAuthorizationContext, ICurrentUserContext) Nobody()
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(x => x.IsAuthenticated).Returns(false);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.ActorName).Returns("system");
        return (principal.Object, user.Object);
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly PlatformDbContext _context;

        public IMongoDatabase Database { get; }

        private World(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            Database = database;
            _context = new PlatformDbContext(mongo.Client, database);
        }

        public static async Task<World> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            return new World(mongo, mongo.CreateDatabase());
        }

        /// <summary>The PRODUCTION in-transaction door: the canonical writer over the real outbox repository.</summary>
        private ITransactionalAuditOutboxWriter Door((ITenantAuthorizationContext Principal, ICurrentUserContext User) who) =>
            new CanonicalTransactionalAuditOutboxWriter(new AuditOutboxRepository(_context), who.Principal, who.User,
                new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()));

        public IGlobalApplicabilityTransactionCoordinator Coordinator((ITenantAuthorizationContext, ICurrentUserContext) who) =>
            new GlobalApplicabilityTransactionCoordinator(new PlatformTransactionExecutor(_context),
                new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), Door(who));

        public async Task<(TenantSubscriptionTransactionWriter Writer, TenantSubscription Subscription, Guid PlanId)> SubscriptionAsync(
            (ITenantAuthorizationContext Principal, ICurrentUserContext User) who, TenantSubscriptionStatus status, DateTimeOffset? periodEnd = null)
        {
            var tenantId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantId);
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true });
            var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(_context),
                new TenantSubscriptionRepository(_context, tenantContext), new TenantRegistryRepository(_context, tenantContext),
                plans.Object, new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), Door(who), who.User);

            await Database.GetCollection<Tenant>("tenants").InsertOneAsync(new Tenant
            {
                Id = tenantId, Code = "AUDIT", Slug = "audit", Name = "Audit", DisplayName = "Audit", Domain = "audit.local", Status = TenantStatus.Active
            });
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid(), TenantId = tenantId, PlanId = planId, Status = status, CurrentPeriodEndUtc = periodEnd,
                RowVersion = Guid.NewGuid().ToByteArray(), UpdatedBy = "platform.admin@di10.test"
            };
            await Database.GetCollection<TenantSubscription>("tenant_subscriptions").InsertOneAsync(subscription);
            return (writer, subscription, planId);
        }

        /// <summary>One pass of the production outbox processor (the worker's unit of work).</summary>
        public Task<int> ProcessAsync()
        {
            var tenantContext = new TenantContext();
            return new AuditOutboxProcessor(new AuditOutboxRepository(_context), new AuditEventRepository(Database, tenantContext), tenantContext,
                new AuditOutboxPayloadMapper(),
                new AuditOutboxWorkerOptions { BatchSize = 10, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
                NullLogger<AuditOutboxProcessor>.Instance).ProcessBatchAsync();
        }

        public async Task<IReadOnlyList<AuditEvent>> AuditEventsAsync() =>
            await Database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).Find(FilterDefinition<AuditEvent>.Empty).ToListAsync();

        public Task<long> DeadLettersAsync() =>
            Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox).CountDocumentsAsync(new BsonDocument("Status", 5));

        public Task<long> CountAsync(string collection) =>
            Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class MongoIntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    {
        public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
        {
            var metadata = new EventMetadata(options.EventId!.Value, @event.EventName, @event.EventVersion,
                options.CorrelationId!.Value, null, options.TenantId, options.Producer!, options.OccurredAtUtc!.Value);
            await context.Database.GetCollection<BsonDocument>("outbox_events").InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument { ["EventId"] = metadata.EventId.ToString() }, cancellationToken: cancellationToken);
            return new EventEnvelope<TEvent>(metadata, @event);
        }
    }
}
