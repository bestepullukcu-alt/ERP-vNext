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

    // ── WP-PLATFORM-AUDIT-INTX-01 FIX1 ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_plan_price_change_is_recorded_with_the_price_before_and_after_and_free_text_by_name_only()
    {
        await using var world = await World.StartAsync();
        var plan = new SubscriptionPlan
        {
            Code = "PRO", Name = "Pro", Description = "old private wording", IsActive = true, PriceMonthly = 10m, Currency = "EUR",
            IncludedModuleKeys = ["CRM"]
        };
        await world.Database.GetCollection<SubscriptionPlan>("platform_subscription_plans").InsertOneAsync(plan);
        var handler = new Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers.UpdateSubscriptionPlanCommandHandler(
            new SubscriptionPlanRepository(world.Context, new TenantContext()),
            NullLogger<Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers.UpdateSubscriptionPlanCommandHandler>.Instance,
            world.Coordinator(Person("platform_admin")), new GlobalApplicabilityStateRepository(world.Context));

        var result = await handler.Handle(new Diten.Platform.Application.Features.SubscriptionPlans.Commands.UpdateSubscriptionPlanCommand(plan.Id,
            new Diten.Platform.Application.Features.SubscriptionPlans.UpdateSubscriptionPlanRequest(
                "PRO", "Pro", "new private wording", true, false, 0, 1000m, null, "EUR", false, null, null, null, ["CRM", "HR"])), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal(10m, Number(audit.BeforeState!["PriceMonthly"]));
        Assert.Equal(1000m, Number(audit.AfterState!["PriceMonthly"]));
        Assert.Equal("CRM", audit.BeforeState["IncludedModuleKeys"]);
        Assert.Equal("CRM,HR", audit.AfterState["IncludedModuleKeys"]);
        Assert.Equal(new[] { "Description", "IncludedModuleKeys", "PriceMonthly" }, Strings(audit.Metadata["ChangedFields"]));
        // A free-text field is named, never copied; an unchanged field is not listed at all.
        Assert.False(audit.BeforeState.ContainsKey("Description"));
        Assert.False(audit.AfterState.ContainsKey("Description"));
        Assert.False(audit.AfterState.ContainsKey("Currency"));
        var stored = (await world.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditEvents).Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).ToJson();
        Assert.DoesNotContain("private wording", stored);
        // FIX1 rule h — the plan's record names its own module.
        Assert.Equal("subscription-billing", audit.SourceModule);
        Assert.Equal(AuditCategory.SubscriptionBilling, audit.Category);
    }

    [Fact]
    public async Task A_catalogue_status_change_is_recorded_with_the_status_before_and_after()
    {
        await using var world = await World.StartAsync();
        var item = new ModuleCatalogItem
        {
            ModuleCode = "CRM", ModuleName = "Crm", DisplayName = "CRM", Domain = "SALES", Service = "CRM",
            Status = ModuleCatalogStatus.Active, IsTenantAssignable = true
        };
        await world.Database.GetCollection<ModuleCatalogItem>("platform_module_catalog").InsertOneAsync(item);
        var handler = new Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers.DeactivateModuleCatalogItemCommandHandler(
            new ModuleCatalogRepository(world.Context, new TenantContext()), world.Coordinator(Person("platform_admin")),
            new GlobalApplicabilityStateRepository(world.Context));

        var result = await handler.Handle(new Diten.Platform.Application.Features.ModuleCatalog.Commands.DeactivateModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal("Active", audit.BeforeState!["Status"]);
        Assert.Equal("Inactive", audit.AfterState!["Status"]);
        Assert.Equal(new[] { "Status" }, Strings(audit.Metadata["ChangedFields"]));
        Assert.Equal("module-catalog", audit.SourceModule);
    }

    [Fact]
    public async Task The_metadata_a_caller_names_reaches_the_global_record_beside_the_version()
    {
        // FIX1 rule e — the descriptor's metadata (the manifest's DeclaredModuleCode, an authenticated producer) is
        // what tells a module self-registration's records apart.
        await using var world = await World.StartAsync();

        await world.Coordinator(Nobody()).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("RegisterModuleManifestCommand", AuditOperation.Update, "ModuleCatalogItem", Guid.NewGuid(),
                SystemActor: "module-manifest-push",
                AuditMetadata: new Dictionary<string, object?> { ["DeclaredModuleCode"] = "PRODUCT-ITEM-SKU-MASTER", ["AuthenticatedProducer"] = "DITENMDMSERVICE" }),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));

        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal("PRODUCT-ITEM-SKU-MASTER", audit.Metadata["DeclaredModuleCode"]);
        Assert.Equal("DITENMDMSERVICE", audit.Metadata["AuthenticatedProducer"]);
        Assert.True(audit.Metadata.ContainsKey("GlobalApplicabilityVersion"));
    }

    [Fact]
    public async Task A_cancellation_at_period_end_is_in_the_state_after()
    {
        // FIX1 rule f — the subscription's recorded state includes CancelAtPeriodEnd and its dates.
        await using var world = await World.StartAsync();
        var periodEnd = new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero);
        var (writer, subscription, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Active, periodEnd);
        subscription.CancelAtPeriodEnd = true;
        subscription.TrialEndDateUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await writer.UpdateAsync(subscription, subscription.RowVersion, planId, "Active",
            "CancelTenantSubscriptionCommand", AuditOperation.Update, false, null, CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal(false, audit.BeforeState!["CancelAtPeriodEnd"]);
        Assert.Equal(true, audit.AfterState!["CancelAtPeriodEnd"]);
        Assert.Equal("2027-01-31T00:00:00.0000000+00:00", audit.AfterState["CurrentPeriodEndUtc"]);
        Assert.Equal("2026-12-01T00:00:00.0000000+00:00", audit.AfterState["TrialEndDateUtc"]);
    }

    [Fact]
    public async Task A_subscription_that_reaches_the_transaction_after_another_went_live_is_refused_with_its_code()
    {
        // FIX1 item 6 — the request's own check ran BEFORE the other subscription committed (that is the window); the
        // writer's check inside the transaction is what refuses it. A Trialing beside an Active is what the unique
        // (TenantId, Status) index alone lets through.
        await using var world = await World.StartAsync();
        var (writer, live, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Active);
        var tenant = await world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == live.TenantId).SingleAsync();
        var late = new TenantSubscription
        {
            Id = Guid.NewGuid(), TenantId = live.TenantId, PlanId = planId, Status = TenantSubscriptionStatus.Trialing,
            UpdatedBy = "platform.admin@di10.test"
        };

        var result = await writer.CreateAsync(late, tenant, new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true },
            "AssignPlanToTenantCommand", AuditOperation.Assign, null, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TenantSubscriptionRefusalCodes.AlreadyCurrent, result.ReasonCode);
        Assert.Equal(1, await world.CountAsync("tenant_subscriptions"));
        Assert.Equal(0, await world.CountAsync("audit_outbox"));
    }

    [Fact]
    public async Task An_intent_naming_an_empty_target_tenant_is_refused_and_nothing_is_written()
    {
        // FIX1 rule g.
        await using var world = await World.StartAsync();
        var door = world.ProductionDoor(Person("platform_admin"), correlation: null);

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() => new PlatformTransactionExecutor(world.Context).ExecuteAsync(
            (session, ct) => door.TryEnqueueAsync(session, new AuditOutboxWriteRequest
            {
                TenantId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), IdempotencyKey = "empty-target", RequestType = "SuspendTenantSubscriptionCommand",
                Operation = AuditOperation.Update, EntityType = "TenantSubscription", EntityId = Guid.NewGuid(),
                Intent = new TransactionOwnedAuditIntent { Category = AuditCategory.SubscriptionBilling, TargetTenantId = Guid.Empty }
            }, ct)));

        Assert.Equal(0, await world.CountAsync("audit_outbox"));
    }

    [Fact]
    public async Task Every_record_one_request_writes_carries_the_requests_correlation_through_both_doors()
    {
        // FIX1 item 5 — RegisterTenantCommand writes two records: one through the central door (AuditService, from the
        // pipeline) and one through the in-transaction door (the subscription it opens). Measured here on the two doors
        // themselves with the request's correlation in scope; without it each keeps its own.
        await using var world = await World.StartAsync();
        var request = Guid.NewGuid();
        var correlation = new FixedCorrelation(request.ToString("N"));
        var central = world.CentralDoor(Person("platform_admin"), correlation);
        var tenant = Guid.NewGuid();

        var appended = await central.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = Guid.NewGuid(), RequestType = "RegisterTenantCommand", ActorType = AuditActorType.PlatformAdministrator,
            Category = AuditCategory.TenantAdministration, EntityType = "Tenant", EntityId = tenant, Operation = AuditOperation.Create,
            TargetTenantId = tenant
        });
        Assert.True(appended.IsEnqueued, appended.Diagnostic);
        await world.Coordinator(Person("platform_admin"), correlation).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));
        await world.Coordinator(Person("platform_admin"), correlation: null).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));

        Assert.Equal(3, await world.ProcessAsync());
        var records = await world.AuditEventsAsync();
        Assert.Equal(2, records.Count(r => r.CorrelationId == request));
        Assert.Single(records, r => r.CorrelationId != request);
        // A correlation header that is not a GUID still gives the same id for the same request.
        Assert.Equal(AuditCorrelation.Resolve("req-42.a", Guid.NewGuid()), AuditCorrelation.Resolve("req-42.a", Guid.NewGuid()));
    }

    [Fact]
    public async Task The_central_record_carries_the_actors_email_and_name_masked_never_in_clear()
    {
        // FIX1 rule a.
        await using var world = await World.StartAsync();
        var central = world.CentralDoor(Person("platform_admin"), correlation: null);
        var tenant = Guid.NewGuid();

        var appended = await central.AppendAsync(new AuditAppendRequest
        {
            CorrelationId = Guid.NewGuid(), RequestType = "UpdateTenantCommand", ActorType = AuditActorType.PlatformAdministrator,
            ActorEmail = "jane.doe@di10.test", ActorDisplayName = "Jane Doe",
            Category = AuditCategory.TenantAdministration, EntityType = "Tenant", EntityId = tenant, Operation = AuditOperation.Update,
            TargetTenantId = tenant
        });

        Assert.True(appended.IsEnqueued, appended.Diagnostic);
        Assert.Equal(1, await world.ProcessAsync());
        var audit = Assert.Single(await world.AuditEventsAsync());
        Assert.Equal("j***@di10.test", audit.ActorEmailMasked);
        Assert.Equal("J***e", audit.ActorDisplayNameMasked);
    }

    [Theory]
    [InlineData("192.168.10.25", "192.168.10.0")]
    [InlineData(" 10.0.0.7 ", "10.0.0.0")]
    [InlineData("2001:db8::1", "2001:****")]
    public void An_ip_address_keeps_only_its_network_part(string address, string masked)
    {
        // FIX1 rule i.
        Assert.Equal(masked, AuditOutboxPayload.MaskIpAddress(address));
    }

    private static decimal Number(object? value) =>
        decimal.Parse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.CultureInfo.InvariantCulture);

    private static string[] Strings(object? value) =>
        ((System.Collections.IEnumerable)value!).Cast<object?>().Select(item => Convert.ToString(item)!).ToArray();

    private sealed class FixedCorrelation(string? id) : Diten.Platform.Common.Observability.ICorrelationContext
    {
        public string? CorrelationId => id;
        public void SetCorrelationId(string correlationId) { }
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

        public PlatformDbContext Context => _context;

        /// <summary>The PRODUCTION in-transaction door: the canonical writer over the real outbox repository.</summary>
        private ITransactionalAuditOutboxWriter Door((ITenantAuthorizationContext Principal, ICurrentUserContext User) who) =>
            ProductionDoor(who, correlation: null);

        public ITransactionalAuditOutboxWriter ProductionDoor((ITenantAuthorizationContext Principal, ICurrentUserContext User) who,
            Diten.Platform.Common.Observability.ICorrelationContext? correlation) =>
            new CanonicalTransactionalAuditOutboxWriter(new AuditOutboxRepository(_context), who.Principal, who.User,
                new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()), correlation);

        /// <summary>The PRODUCTION central door (what AuditBehavior calls), over the same outbox, in the platform context.</summary>
        public IAuditService CentralDoor((ITenantAuthorizationContext Principal, ICurrentUserContext User) who,
            Diten.Platform.Common.Observability.ICorrelationContext? correlation)
        {
            var tenantContext = new TenantContext();
            tenantContext.SetPlatformContext(Guid.Empty);
            return new AuditService(new AuditOutboxRepository(_context), new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()),
                new AuditIdempotencyKeyBuilder(), new AuditRecursionGuard(), tenantContext, who.User, NullLogger<AuditService>.Instance, correlation);
        }

        public IGlobalApplicabilityTransactionCoordinator Coordinator((ITenantAuthorizationContext, ICurrentUserContext) who,
            Diten.Platform.Common.Observability.ICorrelationContext? correlation = null) =>
            new GlobalApplicabilityTransactionCoordinator(new PlatformTransactionExecutor(_context),
                new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), ProductionDoor(who, correlation));

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
