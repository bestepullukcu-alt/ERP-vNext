using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class SubscriptionConcurrencySecurityMongoTests
{
    [Fact]
    public async Task ConcurrentAssign_SameBusinessIdentity_OneWinnerAndExactParticipants()
    {
        await using var fixture = await Fixture.StartAsync();
        var gate = new TwoCallerGate();
        var repository = new GatedSubscriptionRepository(fixture.Subscriptions, gate, gateHasCurrent: true);
        var handlers = new[] { fixture.Assign(repository), fixture.Assign(repository) };
        var request = new AssignPlanToTenantRequest(fixture.Plan.Id, false, null, fixture.Now, fixture.Now.AddMonths(1), "race");

        var results = await Task.WhenAll(handlers.Select(handler => Observe(Task.Run(() =>
            handler.Handle(new AssignPlanToTenantCommand(fixture.Tenant.Id, request), CancellationToken.None)))));

        Assert.Single(results, result => result.Success);
        Assert.Single(results, result => !result.Success && result.StatusCode == 409);
        await fixture.AssertCountsAsync(subscriptions: 1, counter: 1, quotaUsage: 1, quotaEvent: 1, integration: 1, audit: 1);
        var tenant = await fixture.ReloadTenantAsync();
        Assert.Equal(fixture.Plan.Id, tenant.PlanId);
    }

    public static TheoryData<string, TenantSubscriptionStatus, TenantSubscriptionStatus> LifecycleRaces => new()
    {
        { "Activate_vs_Cancel", TenantSubscriptionStatus.Trialing, TenantSubscriptionStatus.Active },
        { "Activate_vs_Suspend", TenantSubscriptionStatus.Trialing, TenantSubscriptionStatus.Active },
        { "Renew_vs_Expire", TenantSubscriptionStatus.Active, TenantSubscriptionStatus.Active }
    };

    [Theory]
    [MemberData(nameof(LifecycleRaces))]
    public async Task ConcurrentLifecycle_ExactPackResult_IsSingleWinner(string pair, TenantSubscriptionStatus initial,
        TenantSubscriptionStatus expectedIfFirstWins)
    {
        await using var fixture = await Fixture.StartAsync(initial);
        var gate = new TwoCallerGate();
        var repository = new GatedSubscriptionRepository(fixture.Subscriptions, gate, gateGet: true);
        var rowVersion = fixture.Subscription!.RowVersion.ToArray();
        Task<Response<NoContent>> left;
        Task<Response<NoContent>> right;
        if (pair == "Activate_vs_Cancel")
        {
            left = Task.Run(() => fixture.Activate(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id,
                new(fixture.Now, fixture.Now.AddMonths(1), rowVersion)), default));
            right = Task.Run(() => fixture.Cancel(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id,
                new("race", false, rowVersion)), default));
        }
        else if (pair == "Activate_vs_Suspend")
        {
            left = Task.Run(() => fixture.Activate(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id,
                new(fixture.Now, fixture.Now.AddMonths(1), rowVersion)), default));
            right = Task.Run(() => fixture.Suspend(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id,
                new("race", rowVersion)), default));
        }
        else
        {
            left = Task.Run(() => fixture.Renew(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id,
                new(fixture.Now.AddMonths(2), rowVersion)), default));
            right = Task.Run(() => fixture.Expire(repository).Handle(new(fixture.Tenant.Id, fixture.Subscription.Id, rowVersion), default));
        }

        var results = await Task.WhenAll(Observe(left), Observe(right));
        Assert.Single(results, result => result.Success);
        Assert.Single(results, result => !result.Success && result.StatusCode is 400 or 409);
        var saved = await fixture.ReloadSubscriptionAsync();
        Assert.Contains(saved.Status, pair switch
        {
            "Activate_vs_Cancel" => new[] { expectedIfFirstWins, TenantSubscriptionStatus.Cancelled },
            "Activate_vs_Suspend" => new[] { expectedIfFirstWins }, // Suspend is invalid from Trialing by pack matrix.
            _ => new[] { TenantSubscriptionStatus.Active, TenantSubscriptionStatus.Expired }
        });
        var activateWon = saved.Status == TenantSubscriptionStatus.Active &&
                          pair.StartsWith("Activate", StringComparison.Ordinal);
        await fixture.AssertCountsAsync(subscriptions: 1, counter: 1,
            quotaUsage: activateWon ? 1 : 0, quotaEvent: activateWon ? 1 : 0, integration: 1, audit: 1);
    }

    [Fact]
    public async Task CrossTenant_ProductionHandler_ReturnsNonDisclosing404AndZeroMutation()
    {
        await using var fixture = await Fixture.StartAsync(TenantSubscriptionStatus.Active);
        var otherTenant = Guid.NewGuid();
        var context = TenantContextFor(otherTenant);
        var repository = new TenantSubscriptionRepository(fixture.Context, context);
        var handler = fixture.Suspend(repository);

        var result = await handler.Handle(new(otherTenant, fixture.Subscription!.Id,
            new("foreign", fixture.Subscription.RowVersion)), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        await fixture.AssertCountsAsync(1, 0, 0, 0, 0, 0);
    }

    public static TheoryData<string> MissingAndSoftDeleted => new() { "soft-deleted-subscription", "missing-subscription", "soft-deleted-tenant", "missing-tenant" };

    [Theory]
    [MemberData(nameof(MissingAndSoftDeleted))]
    public async Task MissingOrSoftDeleted_FailsClosedWithZeroParticipantDelta(string scenario)
    {
        var seedSubscription = !scenario.Contains("missing-subscription", StringComparison.Ordinal);
        await using var fixture = await Fixture.StartAsync(TenantSubscriptionStatus.Active, seedSubscription);
        if (scenario == "soft-deleted-subscription")
            await fixture.Database.GetCollection<TenantSubscription>("tenant_subscriptions")
                .UpdateOneAsync(x => x.Id == fixture.Subscription!.Id, Builders<TenantSubscription>.Update.Set(x => x.IsDeleted, true));
        if (scenario == "soft-deleted-tenant")
            await fixture.Database.GetCollection<Tenant>("tenants")
                .UpdateOneAsync(x => x.Id == fixture.Tenant.Id, Builders<Tenant>.Update.Set(x => x.IsDeleted, true));
        if (scenario == "missing-tenant")
            await fixture.Database.GetCollection<Tenant>("tenants").DeleteOneAsync(x => x.Id == fixture.Tenant.Id);

        var result = await fixture.Suspend(fixture.Subscriptions).Handle(new(fixture.Tenant.Id, fixture.Subscription!.Id,
            new("guard", fixture.Subscription.RowVersion)), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        await fixture.AssertMutationParticipantsZeroAsync();
    }

    [Fact]
    public async Task DuplicateRequest_SamePayloadIsNoDuplicate_ChangedPayloadFailsClosed()
    {
        await using var fixture = await Fixture.StartAsync(TenantSubscriptionStatus.Active);
        var handler = fixture.Cancel(fixture.Subscriptions);
        var first = await handler.Handle(new(fixture.Tenant.Id, fixture.Subscription!.Id,
            new("same", true, fixture.Subscription.RowVersion)), default);
        Assert.True(first.IsSuccessful);
        var saved = await fixture.ReloadSubscriptionAsync();
        var same = await handler.Handle(new(fixture.Tenant.Id, saved.Id, new("same", true, saved.RowVersion)), default);
        var changed = await handler.Handle(new(fixture.Tenant.Id, saved.Id, new("changed", true, fixture.Subscription.RowVersion)), default);
        Assert.True(same.IsSuccessful);
        Assert.False(changed.IsSuccessful);
        await fixture.AssertCountsAsync(1, 1, 0, 0, 1, 1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly ICurrentUserContext _user;
        private readonly IQuotaService _quota;
        private readonly TenantSubscriptionTransactionWriter _writer;
        public IMongoDatabase Database { get; }
        public PlatformDbContext Context { get; }
        public TenantSubscriptionRepository Subscriptions { get; }
        public TenantRegistryRepository Tenants { get; }
        public SubscriptionPlanRepository Plans { get; }
        public Tenant Tenant { get; }
        public SubscriptionPlan Plan { get; }
        public TenantSubscription? Subscription { get; }
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;

        private Fixture(DisposableMongoReplicaSet mongo, IMongoDatabase database, PlatformDbContext context,
            TenantSubscriptionRepository subscriptions, TenantRegistryRepository tenants, SubscriptionPlanRepository plans,
            Tenant tenant, SubscriptionPlan plan, TenantSubscription? subscription, ICurrentUserContext user,
            IQuotaService quota, TenantSubscriptionTransactionWriter writer)
        { _mongo = mongo; Database = database; Context = context; Subscriptions = subscriptions; Tenants = tenants; Plans = plans;
          Tenant = tenant; Plan = plan; Subscription = subscription; _user = user; _quota = quota; _writer = writer; }

        public static async Task<Fixture> StartAsync(TenantSubscriptionStatus? status = null, bool seedSubscription = true)
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            var tenantId = Guid.NewGuid();
            var tenantContext = TenantContextFor(tenantId);
            var subscriptions = new TenantSubscriptionRepository(context, tenantContext);
            var tenants = new TenantRegistryRepository(context, tenantContext);
            var plans = new SubscriptionPlanRepository(context, tenantContext);
            var tenant = new Tenant { Id = tenantId, Code = "RACE", Slug = "race", Name = "Race", DisplayName = "Race",
                Domain = $"{tenantId:N}.local", Status = TenantStatus.Provisioning };
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Code = "RACE", Name = "Race", IsActive = true,
                DefaultQuotas = new Dictionary<string, decimal> { [QuotaKeys.UsersMax] = 1 } };
            await database.GetCollection<Tenant>("tenants").InsertOneAsync(tenant);
            await database.GetCollection<SubscriptionPlan>("platform_subscription_plans").InsertOneAsync(plan);
            await database.GetCollection<TenantSubscription>("tenant_subscriptions").Indexes.CreateOneAsync(
                new CreateIndexModel<TenantSubscription>(Builders<TenantSubscription>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status),
                    new CreateIndexOptions<TenantSubscription> { Unique = true, PartialFilterExpression = Builders<TenantSubscription>.Filter.And(
                        Builders<TenantSubscription>.Filter.Eq(x => x.IsDeleted, false), Builders<TenantSubscription>.Filter.In(x => x.Status, TenantSubscriptionStatuses.Current)) }));
            await database.GetCollection<QuotaUsage>("quota_usages").Indexes.CreateOneAsync(new CreateIndexModel<QuotaUsage>(
                Builders<QuotaUsage>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QuotaKey), new CreateIndexOptions { Unique = true }));
            TenantSubscription? subscription = null;
            if (status.HasValue)
            {
                subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = tenantId, PlanId = plan.Id, Status = status.Value,
                    RowVersion = Guid.NewGuid().ToByteArray(), CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddMonths(-1),
                    CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddMonths(1), UpdatedBy = "race" };
                if (seedSubscription) await database.GetCollection<TenantSubscription>("tenant_subscriptions").InsertOneAsync(subscription);
            }
            var user = new Mock<ICurrentUserContext>(); user.SetupGet(x => x.UserId).Returns(Guid.NewGuid()); user.SetupGet(x => x.ActorName).Returns("race");
            var usage = new QuotaUsageRepository(context, tenantContext);
            var events = new QuotaEventRepository(context, tenantContext);
            var quota = new QuotaService(usage, events, subscriptions, plans, tenants,
                Mock.Of<ITenantModuleEntitlementRepository>(), NullLogger<QuotaService>.Instance);
            var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(context), subscriptions, tenants, plans,
                new EntitlementStateVersionRepository(context), new MongoIntentWriter(context), new MongoAuditWriter(context), user.Object);
            return new Fixture(mongo, database, context, subscriptions, tenants, plans, tenant, plan, subscription, user.Object, quota, writer);
        }

        public AssignPlanToTenantCommandHandler Assign(ITenantSubscriptionRepository repository) => new(Tenants, Plans, repository, _user, _quota, _writer);
        public ActivateTenantSubscriptionCommandHandler Activate(ITenantSubscriptionRepository repository) => new(repository, Tenants, Plans, _user, _quota, _writer);
        public CancelTenantSubscriptionCommandHandler Cancel(ITenantSubscriptionRepository repository) => new(repository, Tenants, Plans, _user, _writer);
        public SuspendTenantSubscriptionCommandHandler Suspend(ITenantSubscriptionRepository repository) => new(repository, Tenants, Plans, _user, _writer);
        public RenewTenantSubscriptionCommandHandler Renew(ITenantSubscriptionRepository repository) => new(repository, Tenants, Plans, _user, _writer);
        public ExpireTenantSubscriptionCommandHandler Expire(ITenantSubscriptionRepository repository) => new(repository, Tenants, Plans, _user, _writer);
        public Task<TenantSubscription> ReloadSubscriptionAsync() => Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.Id == Subscription!.Id).SingleAsync();
        public Task<Tenant> ReloadTenantAsync() => Database.GetCollection<Tenant>("tenants").Find(x => x.Id == Tenant.Id).SingleAsync();
        public async Task AssertCountsAsync(int subscriptions, int counter, int quotaUsage, int quotaEvent, int integration, int audit)
        {
            Assert.Equal(subscriptions, await Count("tenant_subscriptions")); Assert.Equal(1, await Count("tenants"));
            Assert.Equal(counter, await Count(EntitlementStateVersionRepository.CollectionName));
            Assert.Equal(quotaUsage, await Count("quota_usages")); Assert.Equal(quotaEvent, await Count("quota_events"));
            Assert.Equal(integration, await Count("outbox_events")); Assert.Equal(audit, await Count("audit_outbox"));
        }
        public async Task AssertMutationParticipantsZeroAsync()
        { Assert.Equal(0, await Count(EntitlementStateVersionRepository.CollectionName)); Assert.Equal(0, await Count("quota_usages"));
          Assert.Equal(0, await Count("quota_events")); Assert.Equal(0, await Count("outbox_events")); Assert.Equal(0, await Count("audit_outbox")); }
        private Task<long> Count(string name) => Database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class TwoCallerGate
    {
        private int _arrivals;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task MeetAsync()
        { if (Interlocked.Increment(ref _arrivals) == 2) _release.TrySetResult(); await _release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    private sealed record Outcome(bool Success, int StatusCode);
    private static async Task<Outcome> Observe(Task<Response<NoContent>> task)
    {
        try { var response = await task; return new(response.IsSuccessful, response.StatusCode); }
        catch (PlatformTransactionUnavailableException) { return new(false, 503); }
    }
    private static async Task<Outcome> Observe(Task<Response<Guid>> task)
    {
        try { var response = await task; return new(response.IsSuccessful, response.StatusCode); }
        catch (PlatformTransactionUnavailableException) { return new(false, 503); }
    }

    private sealed class GatedSubscriptionRepository(ITenantSubscriptionRepository inner, TwoCallerGate gate,
        bool gateHasCurrent = false, bool gateGet = false) : ITenantSubscriptionRepository
    {
        public Task<TenantSubscription> CreateAsync(IPlatformTransactionSession s, TenantSubscription x, CancellationToken ct = default) => inner.CreateAsync(s, x, ct);
        public Task<TenantSubscription> CreateAsync(TenantSubscription x, CancellationToken ct = default) => inner.CreateAsync(x, ct);
        public Task<TenantSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public async Task<TenantSubscription?> GetByTenantIdAsync(Guid t, Guid id, CancellationToken ct = default)
        { var value = await inner.GetByTenantIdAsync(t, id, ct); if (gateGet) await gate.MeetAsync(); return value; }
        public Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid t, CancellationToken ct = default) => inner.GetCurrentByTenantIdAsync(t, ct);
        public Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid t, CancellationToken ct = default) => inner.GetHistoryByTenantIdAsync(t, ct);
        public async Task<bool> HasCurrentAsync(Guid t, Guid? exclude = null, CancellationToken ct = default)
        { var value = await inner.HasCurrentAsync(t, exclude, ct); if (gateHasCurrent) await gate.MeetAsync(); return value; }
        public Task UpdateAsync(TenantSubscription x, byte[]? row, CancellationToken ct = default) => inner.UpdateAsync(x, row, ct);
        public Task UpdateAsync(IPlatformTransactionSession s, TenantSubscription x, byte[]? row, CancellationToken ct = default) => inner.UpdateAsync(s, x, row, ct);
    }

    private sealed class MongoIntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    {
        public async Task<EventEnvelope<T>> EnqueueAsync<T>(IPlatformTransactionSession session, T value, EventPublishOptions options,
            CancellationToken cancellationToken = default) where T : IIntegrationEvent
        { await context.Database.GetCollection<BsonDocument>("outbox_events").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
              new BsonDocument("TenantId", options.TenantId!.Value.ToString()), cancellationToken: cancellationToken);
          return new(new(options.EventId!.Value, value.EventName, value.EventVersion, options.CorrelationId!.Value, null,
              options.TenantId, options.Producer!, options.OccurredAtUtc!.Value), value); }
    }
    private sealed class MongoAuditWriter(IPlatformDbContext context) : ITransactionalAuditOutboxWriter
    { public async Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default)
      { await context.Database.GetCollection<BsonDocument>("audit_outbox").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
            new BsonDocument("TenantId", request.TenantId.ToString()), cancellationToken: ct); return true; } }
    private static TenantContext TenantContextFor(Guid tenantId) { var value = new TenantContext(); value.SetTenant(tenantId); return value; }
}
