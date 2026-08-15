using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Application.Tests.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.GlobalApplicability;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class GlobalApplicabilityTransactionMongoTests
{
    [Fact]
    public async Task EffectiveChange_CommitsAllFiveParticipants_WithExactPlusOne()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);
        var plan = Plan("PRO", ["PPM"]);

        var result = await fixture.Coordinator.ExecuteAsync(
            new("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", plan.Id),
            async (session, ct) =>
            {
                await fixture.Plans.CreateAsync(session, plan, ct);
                return new GlobalApplicabilityMutation<Guid>(plan.Id, true,
                    (s, version, token) => fixture.State.UpsertSubscriptionPlanAsync(s, plan, version, token));
            });

        Assert.Equal(plan.Id, result);
        await fixture.AssertCountsAsync(plans: 1, modules: 0, projections: 1, counters: 1, integration: 1, audit: 1);
        Assert.Equal(1L, await fixture.GlobalCounterValueAsync());
        Assert.Equal(1L, await fixture.ProjectionVersionAsync($"plan:{plan.Id:D}"));
    }

    [Fact]
    public async Task NoOp_WritesExactZeroParticipants()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);

        var result = await fixture.Coordinator.ExecuteAsync(
            new("SeedDefaultSubscriptionPlansCommand", AuditOperation.Create, "SubscriptionPlan", Guid.NewGuid()),
            (_, _) => Task.FromResult(new GlobalApplicabilityMutation<bool>(false, false)));

        Assert.False(result);
        await fixture.AssertCountsAsync(0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public async Task ConcurrentEffectiveChanges_DoNotLoseGlobalIncrement()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);
        await fixture.Database.CreateCollectionAsync(EntitlementStateVersionRepository.CollectionName);
        await fixture.Database.CreateCollectionAsync(GlobalApplicabilityStateRepository.CollectionName);

        var plans = new[] { Plan("ONE", ["PPM"]), Plan("TWO", ["MDM"]) };
        await Task.WhenAll(plans.Select(plan => fixture.Coordinator.ExecuteAsync(
            new("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", plan.Id),
            async (session, ct) =>
            {
                await fixture.Plans.CreateAsync(session, plan, ct);
                return new GlobalApplicabilityMutation<bool>(true, true,
                    (s, version, token) => fixture.State.UpsertSubscriptionPlanAsync(s, plan, version, token));
            })));

        Assert.Equal(2L, await fixture.GlobalCounterValueAsync());
        var projectionDocuments = await fixture.Database.GetCollection<BsonDocument>(GlobalApplicabilityStateRepository.CollectionName)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var versions = projectionDocuments.Select(x => x["GlobalVersion"].AsInt64).ToList();
        Assert.Equal(new long[] { 1, 2 }, versions.Order());
    }

    [Fact]
    public async Task AuditParticipantFailure_RollsBackBusinessProjectionCounterAndIntegration()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo, audit: new RejectingAuditWriter());
        var plan = Plan("ROLLBACK", ["PPM"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.ExecuteAsync(
            new("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", plan.Id),
            async (session, ct) =>
            {
                await fixture.Plans.CreateAsync(session, plan, ct);
                return new GlobalApplicabilityMutation<bool>(true, true,
                    (s, version, token) => fixture.State.UpsertSubscriptionPlanAsync(s, plan, version, token));
            }));

        await fixture.AssertCountsAsync(0, 0, 0, 0, 0, 0);
    }

    [Theory]
    [InlineData("business")]
    [InlineData("counter")]
    [InlineData("projection")]
    [InlineData("integration")]
    public async Task EveryPreAuditParticipantFailure_RollsBackAllEarlierParticipants(string faultAfter)
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(
            mongo,
            versions: faultAfter == "counter" ? new RejectingVersionRepository() : null,
            integration: faultAfter == "integration" ? new RejectingIntegrationWriter() : null);
        var plan = Plan($"FAULT-{faultAfter}", ["PPM"]);

        await Assert.ThrowsAsync<InjectedParticipantFailure>(() => fixture.Coordinator.ExecuteAsync(
            new("CreateSubscriptionPlanCommand", AuditOperation.Create, "SubscriptionPlan", plan.Id),
            async (session, ct) =>
            {
                await fixture.Plans.CreateAsync(session, plan, ct);
                if (faultAfter == "business") throw new InjectedParticipantFailure();
                return new GlobalApplicabilityMutation<bool>(true, true,
                    async (s, version, token) =>
                    {
                        await fixture.State.UpsertSubscriptionPlanAsync(s, plan, version, token);
                        if (faultAfter == "projection") throw new InjectedParticipantFailure();
                    });
            }));

        await fixture.AssertCountsAsync(0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public async Task SeedCommand_FirstRunIsTransactional_AndIdenticalSecondRunIsExactZero()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);
        var handler = new SeedDefaultSubscriptionPlansCommandHandler(
            fixture.Plans,
            NullLogger<SeedDefaultSubscriptionPlansCommandHandler>.Instance,
            fixture.Coordinator,
            fixture.State);

        var first = await handler.Handle(new(), default);
        Assert.True(first.IsSuccessful);
        await fixture.AssertCountsAsync(4, 0, 4, 1, 4, 4);
        Assert.Equal(4L, await fixture.GlobalCounterValueAsync());

        var second = await handler.Handle(new(), default);
        Assert.True(second.IsSuccessful);
        await fixture.AssertCountsAsync(4, 0, 4, 1, 4, 4);
        Assert.Equal(4L, await fixture.GlobalCounterValueAsync());
    }

    [Fact]
    public async Task ReverseOrderedEventDelivery_CannotRegressAuthoritativeCounterOrProjection()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);
        var plan = Plan("ORDERED", ["PPM"]);

        foreach (var moduleKeys in new[] { new[] { "PPM" }, new[] { "PPM", "MDM" } })
        {
            plan.IncludedModuleKeys = moduleKeys.ToList();
            await fixture.Coordinator.ExecuteAsync(
                new("UpdateSubscriptionPlanCommand", AuditOperation.Update, "SubscriptionPlan", plan.Id),
                async (session, ct) =>
                {
                    if (moduleKeys.Length == 1) await fixture.Plans.CreateAsync(session, plan, ct);
                    else await fixture.Plans.UpdateAsync(session, plan, ct);
                    return new GlobalApplicabilityMutation<bool>(true, true,
                        (s, version, token) => fixture.State.UpsertSubscriptionPlanAsync(s, plan, version, token));
                });
        }

        var events = await fixture.Database.GetCollection<OutboxEvent>("outbox_events")
            .Find(FilterDefinition<OutboxEvent>.Empty).ToListAsync();
        var deliveredVersions = events
            .Select(x => JsonSerializer.Deserialize<GlobalApplicabilityChangedV1>(x.PayloadJson)!.GlobalApplicabilityVersion)
            .OrderDescending()
            .ToArray();

        Assert.Equal(new ulong[] { 2, 1 }, deliveredVersions);
        Assert.Equal(2L, await fixture.GlobalCounterValueAsync());
        Assert.Equal(2L, await fixture.ProjectionVersionAsync($"plan:{plan.Id:D}"));
    }

    [Fact]
    public async Task BulkDelete_IsAllOrNothing_AndEachModuleGetsOwnVersionAndIntents()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var fixture = await Fixture.CreateAsync(mongo);
        var first = Module("FIRST");
        var second = Module("SECOND");
        await fixture.Database.GetCollection<ModuleCatalogItem>("platform_module_catalog")
            .InsertManyAsync([first, second]);
        var handler = new BulkDeleteModuleCatalogItemsCommandHandler(fixture.Modules, fixture.Coordinator, fixture.State);

        var failed = await handler.Handle(new BulkDeleteModuleCatalogItemsCommand([first.Id, Guid.NewGuid()]), default);
        Assert.False(failed.IsSuccessful);
        Assert.Equal(2, await fixture.Database.GetCollection<ModuleCatalogItem>("platform_module_catalog")
            .CountDocumentsAsync(x => !x.IsDeleted));
        await fixture.AssertCountsAsync(0, 2, 0, 0, 0, 0);

        var success = await handler.Handle(new BulkDeleteModuleCatalogItemsCommand([first.Id, second.Id]), default);
        Assert.True(success.IsSuccessful);
        Assert.Equal(0, await fixture.Database.GetCollection<ModuleCatalogItem>("platform_module_catalog")
            .CountDocumentsAsync(x => !x.IsDeleted));
        await fixture.AssertCountsAsync(0, 2, 2, 1, 2, 2);
        Assert.Equal(2L, await fixture.GlobalCounterValueAsync());
    }

    private static SubscriptionPlan Plan(string code, IReadOnlyList<string> modules) => new()
    {
        Code = code, Name = code, IsActive = true, IncludedModuleKeys = modules.ToList()
    };

    private static ModuleCatalogItem Module(string code) => new()
    {
        ModuleCode = code, ModuleName = code, DisplayName = code,
        Domain = "PLATFORM", Service = "PLATFORM", Status = ModuleCatalogStatus.Active
    };

    private sealed class RejectingAuditWriter : ITransactionalAuditOutboxWriter
    {
        public Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request,
            CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class InjectedParticipantFailure : Exception;

    private sealed class RejectingVersionRepository : IEntitlementStateVersionRepository
    {
        public Task<ulong> IncrementGlobalApplicabilityVersionAsync(IPlatformTransactionSession session, CancellationToken cancellationToken = default) =>
            throw new InjectedParticipantFailure();
        public Task<ulong> IncrementPhysicalEntitlementVersionAsync(IPlatformTransactionSession session, Guid tenantId, string moduleCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ulong> IncrementSubscriptionSelectionVersionAsync(IPlatformTransactionSession session, Guid tenantId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RejectingIntegrationWriter : ITransactionalIntegrationEventWriter
    {
        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent =>
            throw new InjectedParticipantFailure();
    }

    private sealed class TestIntegrationWriter(ITransactionalOutboxEventWriter outbox) : ITransactionalIntegrationEventWriter
    {
        public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session,
            TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            var metadata = new EventMetadata(options.EventId ?? Guid.NewGuid(), @event.EventName, @event.EventVersion,
                options.CorrelationId ?? Guid.NewGuid(), options.CausationId, options.TenantId,
                options.Producer ?? "Diten.Platform", options.OccurredAtUtc ?? DateTimeOffset.UtcNow);
            var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
            var inserted = await outbox.EnqueueAsync(session,
                new EventOutboxWriteRequest(metadata, payload, TrustedTransportMetadata.Empty), cancellationToken);
            Assert.Equal(EventOutboxWriteResult.Inserted, inserted);
            return new(metadata, @event);
        }
    }

    private sealed class Fixture
    {
        public required IMongoDatabase Database { get; init; }
        public required ITransactionalSubscriptionPlanRepository Plans { get; init; }
        public required ITransactionalModuleCatalogRepository Modules { get; init; }
        public required IGlobalApplicabilityStateRepository State { get; init; }
        public required IGlobalApplicabilityTransactionCoordinator Coordinator { get; init; }

        public static async Task<Fixture> CreateAsync(DisposableMongoReplicaSet mongo,
            ITransactionalAuditOutboxWriter? audit = null,
            IEntitlementStateVersionRepository? versions = null,
            ITransactionalIntegrationEventWriter? integration = null)
        {
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            var tenant = new TenantContext();
            var plans = new SubscriptionPlanRepository(context, tenant);
            var modules = new ModuleCatalogRepository(context, tenant);
            var state = new GlobalApplicabilityStateRepository(context);
            var outbox = new OutboxEventRepository(context, tenant);
            await database.GetCollection<OutboxEvent>("outbox_events").Indexes.CreateOneAsync(
                new CreateIndexModel<OutboxEvent>(Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventId),
                    new CreateIndexOptions { Unique = true }));
            var coordinator = new GlobalApplicabilityTransactionCoordinator(
                new PlatformTransactionExecutor(context), versions ?? new EntitlementStateVersionRepository(context),
                integration ?? new TestIntegrationWriter(outbox), audit ?? new AuditOutboxRepository(context));
            return new() { Database = database, Plans = plans, Modules = modules, State = state, Coordinator = coordinator };
        }

        public async Task AssertCountsAsync(long plans, long modules, long projections, long counters,
            long integration, long audit)
        {
            Assert.Equal(plans, await Database.GetCollection<BsonDocument>("platform_subscription_plans").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(modules, await Database.GetCollection<BsonDocument>("platform_module_catalog").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(projections, await Database.GetCollection<BsonDocument>(GlobalApplicabilityStateRepository.CollectionName).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(counters, await Database.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(integration, await Database.GetCollection<BsonDocument>("outbox_events").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(audit, await Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }

        public async Task<long> GlobalCounterValueAsync() =>
            (await Database.GetCollection<BsonDocument>(EntitlementStateVersionRepository.CollectionName)
                .Find(x => x["_id"] == "global:catalog-applicability").SingleAsync())["Value"].AsInt64;

        public async Task<long> ProjectionVersionAsync(string id) =>
            (await Database.GetCollection<BsonDocument>(GlobalApplicabilityStateRepository.CollectionName)
                .Find(x => x["_id"] == id).SingleAsync())["GlobalVersion"].AsInt64;
    }
}
