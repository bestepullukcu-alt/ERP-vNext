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

        // FIX2 item 7 — an invalid intent is its own (sub)type of refusal: a programming error, answered 500 not 503.
        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(() => new PlatformTransactionExecutor(world.Context).ExecuteAsync(
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

    // ── INTX FIX2 ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_assignments_racing_on_one_tenant_leave_one_live_subscription_and_the_late_one_is_stale()
    {
        // FIX2 A2 — built deterministically: B reads "no live subscription" INSIDE its transaction, then — before B
        // writes anything — A assigns and commits. B must not commit a second live subscription. The live-check read
        // pins B's snapshot, so B's write of the tenant meets A's committed change and B is answered as stale.
        await using var world = await World.StartAsync();
        var paused = new TaskCompletionSource();
        var resume = new TaskCompletionSource();
        var (writerB, existing, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Cancelled,
            wrapSubscriptions: inner => new PauseAfterLiveCheck(inner, paused, resume));
        var tenantId = existing.TenantId;
        var plan = new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true };
        Tenant Fresh() => world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == tenantId).Single();

        var b = Task.Run(() => writerB.CreateAsync(NewSubscription(tenantId, planId, TenantSubscriptionStatus.Trialing), Fresh(), plan,
            "AssignPlanToTenantCommand", AuditOperation.Assign, null, CancellationToken.None));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var a = await world.WriterFor(tenantId, planId, Person("platform_admin")).CreateAsync(NewSubscription(tenantId, planId, TenantSubscriptionStatus.Active),
            Fresh(), plan, "AssignPlanToTenantCommand", AuditOperation.Assign, null, CancellationToken.None);
        resume.SetResult();
        var late = await b.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(a.IsSuccessful, string.Join(";", a.Errors ?? []));
        Assert.False(late.IsSuccessful);
        Assert.Equal(409, late.StatusCode);
        Assert.Equal(TenantSubscriptionRefusalCodes.Stale, late.ReasonCode);
        var live = (await world.Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.TenantId == tenantId).ToListAsync())
            .Count(x => TenantSubscriptionStatuses.Current.Contains(x.Status));
        Assert.Equal(1, live);
        Assert.Equal(TenantSubscriptionStatus.Active, Fresh().SubscriptionStatus);
    }

    [Fact]
    public async Task An_assignment_writes_the_tenant_as_its_transaction_reads_it_not_the_copy_the_request_read_earlier()
    {
        // FIX2 A2 — the request read the tenant before its transaction began; a change committed in between must not be
        // written back over by that older copy.
        await using var world = await World.StartAsync();
        var (writer, cancelled, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Cancelled);
        var tenantId = cancelled.TenantId;
        var readEarlier = await world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == tenantId).SingleAsync();
        await world.Database.GetCollection<Tenant>("tenants").UpdateOneAsync(x => x.Id == tenantId,
            Builders<Tenant>.Update.Set(x => x.DisplayName, "renamed after the request read it"));

        var result = await writer.CreateAsync(NewSubscription(tenantId, planId, TenantSubscriptionStatus.Active), readEarlier,
            new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true }, "AssignPlanToTenantCommand", AuditOperation.Assign, null, CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        var stored = await world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == tenantId).SingleAsync();
        Assert.Equal("renamed after the request read it", stored.DisplayName);
        Assert.Equal(planId, stored.PlanId);
    }

    [Fact]
    public async Task A_subscription_update_whose_tenant_another_write_changed_meanwhile_is_answered_stale_with_its_code()
    {
        // FIX2 rule :225 — the update path's stale answer. B's transaction has begun (its subscription write pins the
        // snapshot); the tenant document is then changed and committed elsewhere; B's tenant write meets that change.
        await using var world = await World.StartAsync();
        var paused = new TaskCompletionSource();
        var resume = new TaskCompletionSource();
        var (writer, existing, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Trialing,
            wrapSubscriptions: inner => new PauseAfterSessionUpdate(inner, paused, resume));
        var before = await world.Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.Id == existing.Id).SingleAsync();
        existing.Status = TenantSubscriptionStatus.Active;

        var b = Task.Run(() => writer.UpdateAsync(existing, existing.RowVersion, planId, "Trialing", "ActivateTenantSubscriptionCommand",
            AuditOperation.Activate, true, null, CancellationToken.None));
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await world.Database.GetCollection<Tenant>("tenants").UpdateOneAsync(x => x.Id == existing.TenantId,
            Builders<Tenant>.Update.Set(x => x.DisplayName, "changed meanwhile"));
        resume.SetResult();
        var late = await b.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(late.IsSuccessful);
        Assert.Equal(409, late.StatusCode);
        Assert.Equal(TenantSubscriptionRefusalCodes.Stale, late.ReasonCode);
        var stored = await world.Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.Id == existing.Id).SingleAsync();
        Assert.Equal(before.Status, stored.Status);
        Assert.Equal(0, await world.CountAsync("audit_outbox"));
    }

    [Fact]
    public async Task A_participant_refusal_keeps_its_code_on_both_writer_paths()
    {
        // FIX2 rules :76 / :101 — a quota participant's coded refusal reaches the caller with its code.
        await using var world = await World.StartAsync();
        var (writer, existing, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Trialing);
        Task<Response<NoContent>> Refuse(IPlatformTransactionSession _, TenantSubscription __, SubscriptionPlan ___, CancellationToken ____) =>
            Task.FromResult(Response<NoContent>.Fail("limit", 409, "QUOTA_LIMIT_EXCEEDED"));
        var tenant = await world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == existing.TenantId).SingleAsync();
        var plan = new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true };

        // an empty tenant for the create path (the seeded one already has a live subscription)
        var (createWriter, cancelled, createPlan) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Cancelled);
        var createTenant = await world.Database.GetCollection<Tenant>("tenants").Find(x => x.Id == cancelled.TenantId).SingleAsync();
        var created = await createWriter.CreateAsync(NewSubscription(cancelled.TenantId, createPlan, TenantSubscriptionStatus.Active), createTenant,
            new SubscriptionPlan { Id = createPlan, Code = "PRO", Name = "Pro", IsActive = true }, "AssignPlanToTenantCommand", AuditOperation.Assign, Refuse, CancellationToken.None);
        existing.Status = TenantSubscriptionStatus.Active;
        var updated = await writer.UpdateAsync(existing, existing.RowVersion, planId, "Trialing", "ActivateTenantSubscriptionCommand", AuditOperation.Activate, true, Refuse, CancellationToken.None);

        Assert.Equal("QUOTA_LIMIT_EXCEEDED", created.ReasonCode);
        Assert.Equal("QUOTA_LIMIT_EXCEEDED", updated.ReasonCode);
        Assert.Equal(0, await world.CountAsync("audit_outbox"));
    }

    [Fact]
    public async Task The_integration_event_and_the_audit_record_carry_one_correlation()
    {
        // FIX2 A6 — measured on both callers: the subscription writer and the global coordinator.
        await using var world = await World.StartAsync();
        var request = Guid.NewGuid();
        var correlation = new FixedCorrelation(request.ToString("D"));
        var (writer, subscription, planId) = await world.SubscriptionAsync(Person("platform_admin"), TenantSubscriptionStatus.Active, correlation: correlation);
        subscription.Status = TenantSubscriptionStatus.Suspended;
        Assert.True((await writer.UpdateAsync(subscription, subscription.RowVersion, planId, "Active", "SuspendTenantSubscriptionCommand",
            AuditOperation.Suspend, false, null, CancellationToken.None)).IsSuccessful);
        await world.Coordinator(Person("platform_admin"), correlation).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));
        // and with no request in scope: still ONE id for both
        await world.Coordinator(Person("platform_admin"), correlation: null).ExecuteAsync(
            new GlobalApplicabilityMutationDescriptor("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));

        Assert.Equal(3, await world.ProcessAsync());
        var audits = (await world.AuditEventsAsync()).Select(a => a.CorrelationId.ToString()).OrderBy(x => x).ToList();
        var events = (await world.Database.GetCollection<BsonDocument>("outbox_events").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            .Select(e => Guid.Parse(e["CorrelationId"].AsString).ToString()).OrderBy(x => x).ToList();
        Assert.Equal(audits, events);
        Assert.Equal(2, audits.Count(x => x == request.ToString()));
    }

    [Fact]
    public async Task A_client_chosen_correlation_never_becomes_the_records_correlation_and_is_kept_as_what_the_client_said()
    {
        // FIX2 A3 — two requests with the SAME client header: two record correlations (the server's), both records
        // carrying the client's value as metadata only, through both doors.
        await using var world = await World.StartAsync();
        var first = new FixedCorrelation(Guid.NewGuid().ToString("N"), client: "same-client-header");
        var second = new FixedCorrelation(Guid.NewGuid().ToString("N"), client: "same-client-header");
        var tenant = Guid.NewGuid();

        foreach (var correlation in new[] { first, second })
        {
            Assert.True((await world.CentralDoor(Person("platform_admin"), correlation).AppendAsync(new AuditAppendRequest
            {
                CorrelationId = Guid.NewGuid(), RequestType = "UpdateTenantCommand", ActorType = AuditActorType.PlatformAdministrator,
                Category = AuditCategory.TenantAdministration, EntityType = "Tenant", EntityId = tenant, Operation = AuditOperation.Update, TargetTenantId = tenant
            })).IsEnqueued);
            await world.Coordinator(Person("platform_admin"), correlation).ExecuteAsync(
                new GlobalApplicabilityMutationDescriptor("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
                (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(true, true, (_, _, _) => Task.CompletedTask)));
        }

        Assert.Equal(4, await world.ProcessAsync());
        var records = await world.AuditEventsAsync();
        Assert.Equal(2, records.Select(r => r.CorrelationId).Distinct().Count());
        Assert.All(records, r => Assert.Equal("same-client-header", r.Metadata[AuditCorrelation.ClientCorrelationMetadataKey]));
        Assert.DoesNotContain(records, r => r.CorrelationId == AuditCorrelation.Resolve("same-client-header", Guid.Empty));
        // an unsafe client value is not carried at all
        Assert.Null(AuditCorrelation.ClientValue("bad value<script>"));
        Assert.Null(AuditCorrelation.ClientValue(new string('a', 129)));
    }

    private static TenantSubscription NewSubscription(Guid tenantId, Guid planId, TenantSubscriptionStatus status) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, PlanId = planId, Status = status, UpdatedBy = "platform.admin@di10.test"
    };

    /// <summary>Holds the transaction right after its live-subscription check until the test lets it go.</summary>
    private sealed class PauseAfterLiveCheck(ITenantSubscriptionRepository inner, TaskCompletionSource paused, TaskCompletionSource resume) : ITenantSubscriptionRepository
    {
        public async Task<bool> HasCurrentAsync(IPlatformTransactionSession session, Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default)
        {
            var answer = await inner.HasCurrentAsync(session, tenantId, excludeSubscriptionId, ct);
            paused.TrySetResult();
            await resume.Task;
            return answer;
        }

        public async Task<bool> HasCurrentAsync(Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default)
        {
            var answer = await inner.HasCurrentAsync(tenantId, excludeSubscriptionId, ct);
            paused.TrySetResult();
            await resume.Task;
            return answer;
        }

        public Task<TenantSubscription> CreateAsync(IPlatformTransactionSession session, TenantSubscription subscription, CancellationToken ct = default) => inner.CreateAsync(session, subscription, ct);
        public Task<TenantSubscription> CreateAsync(TenantSubscription subscription, CancellationToken ct = default) => inner.CreateAsync(subscription, ct);
        public Task<TenantSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default) => inner.GetByTenantIdAsync(tenantId, subscriptionId, ct);
        public Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid tenantId, CancellationToken ct = default) => inner.GetCurrentByTenantIdAsync(tenantId, ct);
        public Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid tenantId, CancellationToken ct = default) => inner.GetHistoryByTenantIdAsync(tenantId, ct);
        public Task UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default) => inner.UpdateAsync(subscription, expectedRowVersion, ct);
        public Task UpdateAsync(IPlatformTransactionSession session, TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default) => inner.UpdateAsync(session, subscription, expectedRowVersion, ct);
    }

    /// <summary>Holds the transaction right after its subscription write until the test lets it go.</summary>
    private sealed class PauseAfterSessionUpdate(ITenantSubscriptionRepository inner, TaskCompletionSource paused, TaskCompletionSource resume) : ITenantSubscriptionRepository
    {
        public async Task UpdateAsync(IPlatformTransactionSession session, TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default)
        {
            await inner.UpdateAsync(session, subscription, expectedRowVersion, ct);
            paused.TrySetResult();
            await resume.Task;
        }

        public Task<bool> HasCurrentAsync(IPlatformTransactionSession session, Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default) => inner.HasCurrentAsync(session, tenantId, excludeSubscriptionId, ct);
        public Task<bool> HasCurrentAsync(Guid tenantId, Guid? excludeSubscriptionId = null, CancellationToken ct = default) => inner.HasCurrentAsync(tenantId, excludeSubscriptionId, ct);
        public Task<TenantSubscription> CreateAsync(IPlatformTransactionSession session, TenantSubscription subscription, CancellationToken ct = default) => inner.CreateAsync(session, subscription, ct);
        public Task<TenantSubscription> CreateAsync(TenantSubscription subscription, CancellationToken ct = default) => inner.CreateAsync(subscription, ct);
        public Task<TenantSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct = default) => inner.GetByTenantIdAsync(tenantId, subscriptionId, ct);
        public Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid tenantId, CancellationToken ct = default) => inner.GetCurrentByTenantIdAsync(tenantId, ct);
        public Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid tenantId, CancellationToken ct = default) => inner.GetHistoryByTenantIdAsync(tenantId, ct);
        public Task UpdateAsync(TenantSubscription subscription, byte[]? expectedRowVersion, CancellationToken ct = default) => inner.UpdateAsync(subscription, expectedRowVersion, ct);
    }

    private static decimal Number(object? value) =>
        decimal.Parse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!, System.Globalization.CultureInfo.InvariantCulture);

    private static string[] Strings(object? value) =>
        ((System.Collections.IEnumerable)value!).Cast<object?>().Select(item => Convert.ToString(item)!).ToArray();

    private sealed class FixedCorrelation(string? id, string? client = null) : Diten.Platform.Common.Observability.ICorrelationContext
    {
        public string? CorrelationId => id;
        public string? ClientCorrelationId => client;
        public void SetCorrelationId(string correlationId) { }
        public void SetClientCorrelationId(string clientCorrelationId) { }
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
                new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), ProductionDoor(who, correlation), correlation);

        /// <summary>A second writer over the SAME world (another request on the same tenant): the same tenant, plan and store.</summary>
        public TenantSubscriptionTransactionWriter WriterFor(Guid tenantId, Guid planId, (ITenantAuthorizationContext Principal, ICurrentUserContext User) who)
        {
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantId);
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true });
            return new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(_context),
                new TenantSubscriptionRepository(_context, tenantContext), new TenantRegistryRepository(_context, tenantContext),
                plans.Object, new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), Door(who), who.User);
        }

        public async Task<(TenantSubscriptionTransactionWriter Writer, TenantSubscription Subscription, Guid PlanId)> SubscriptionAsync(
            (ITenantAuthorizationContext Principal, ICurrentUserContext User) who, TenantSubscriptionStatus status, DateTimeOffset? periodEnd = null,
            Diten.Platform.Common.Observability.ICorrelationContext? correlation = null,
            Func<ITenantSubscriptionRepository, ITenantSubscriptionRepository>? wrapSubscriptions = null)
        {
            var tenantId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantId);
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { Id = planId, Code = "PRO", Name = "Pro", IsActive = true });
            ITenantSubscriptionRepository subscriptions = new TenantSubscriptionRepository(_context, tenantContext);
            if (wrapSubscriptions is not null) subscriptions = wrapSubscriptions(subscriptions);
            var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(_context),
                subscriptions, new TenantRegistryRepository(_context, tenantContext),
                plans.Object, new EntitlementStateVersionRepository(_context), new MongoIntentWriter(_context), ProductionDoor(who, correlation), who.User, correlation);

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
                new BsonDocument { ["EventId"] = metadata.EventId.ToString(), ["CorrelationId"] = metadata.CorrelationId.ToString() }, cancellationToken: cancellationToken);
            return new EventEnvelope<TEvent>(metadata, @event);
        }
    }
}
