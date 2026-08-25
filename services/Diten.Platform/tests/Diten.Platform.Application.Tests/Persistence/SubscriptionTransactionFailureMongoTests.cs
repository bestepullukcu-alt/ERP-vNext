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
public sealed class SubscriptionTransactionFailureMongoTests
{
    [Fact]
    public async Task UnknownBeforeCommit_BodyOnce_CommitTwice_SameSession_AllParticipantsExactOne()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(beforeUnknown: [1]));
        var result = await fixture.CreateAsync();
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1, 2);
        await fixture.AssertParticipantCountsAsync(1);
    }

    [Fact]
    public async Task UnknownBeforeCommit_Exhaustion_BodyOnce_CommitThree_503AndZeroResidue()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(beforeUnknown: [1, 2, 3]));
        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(fixture.CreateAsync);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1, 2, 3);
        await fixture.AssertParticipantCountsAsync(0);
    }

    [Fact]
    public async Task UnknownAfterDurableCommit_BodyOnce_CommitTwice_ReconcilesExactOne()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(afterUnknown: [1]));
        var result = await fixture.CreateAsync();
        Assert.True(result.IsSuccessful);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1, 2);
        Assert.Equal(1, fixture.Probe.AfterDurableUnknownCount);
        await fixture.AssertParticipantCountsAsync(1);
    }

    [Fact]
    public async Task UnknownAfterDurableCommit_Exhaustion_Is503ButDurableStateRemainsExactOne()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(afterUnknown: [1, 2, 3]));
        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(fixture.CreateAsync);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1, 2, 3);
        await fixture.AssertParticipantCountsAsync(1);
    }

    [Fact]
    public async Task TransientBodyFailure_RetriesOnNewSession_AbortsFirstAndCommitsExactOne()
    {
        await using var fixture = await FailureFixture.StartAsync(transientQuotaAttempts: [1]);
        var result = await fixture.CreateAsync();
        Assert.True(result.IsSuccessful);
        Assert.Equal(2, fixture.SubscriptionWrites);
        Assert.Equal(2, fixture.BodySessions.Distinct().Count());
        fixture.Probe.AssertAttempts(1);
        await fixture.AssertParticipantCountsAsync(1);
    }

    [Fact]
    public async Task TransientBodyFailure_MaximumThreeAttempts_503AndZeroResidue()
    {
        await using var fixture = await FailureFixture.StartAsync(transientQuotaAttempts: [1, 2, 3]);
        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(fixture.CreateAsync);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(3, fixture.SubscriptionWrites);
        Assert.Equal(3, fixture.BodySessions.Distinct().Count());
        Assert.Empty(fixture.Probe.CommitAttempts);
        await fixture.AssertParticipantCountsAsync(0);
    }

    [Fact]
    public async Task CancellationBeforeBody_PropagatesAndLeavesZeroResidue()
    {
        await using var fixture = await FailureFixture.StartAsync();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.CreateAsync(cts.Token));
        Assert.Equal(0, fixture.SubscriptionWrites);
        await fixture.AssertParticipantCountsAsync(0);
    }

    [Fact]
    public async Task CancellationDuringParticipants_PropagatesAndRollsBackAll()
    {
        await using var fixture = await FailureFixture.StartAsync(cancelDuringQuota: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(fixture.CreateAsync);
        Assert.Equal(1, fixture.SubscriptionWrites);
        Assert.Empty(fixture.Probe.CommitAttempts);
        await fixture.AssertParticipantCountsAsync(0);
    }

    [Fact]
    public async Task CancellationBeforeCommit_PropagatesAndRollsBackAll()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(cancelBefore: [1]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(fixture.CreateAsync);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1);
        await fixture.AssertParticipantCountsAsync(0);
    }

    [Fact]
    public async Task CallerCancellationAfterDurableCommit_PropagatesWithExactOneDurableState()
    {
        await using var fixture = await FailureFixture.StartAsync(new CommitProbe(cancelAfter: [1]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(fixture.CreateAsync);
        Assert.Equal(1, fixture.SubscriptionWrites);
        fixture.Probe.AssertAttempts(1);
        await fixture.AssertParticipantCountsAsync(1);
    }

    private sealed class FailureFixture : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TenantSubscriptionTransactionWriter _writer;
        private readonly Tenant _tenant;
        private readonly SubscriptionPlan _plan;
        private readonly HashSet<int> _transientQuotaAttempts;
        private readonly bool _cancelDuringQuota;
        private int _quotaAttempt;
        public IMongoDatabase Database { get; }
        public PlatformDbContext Context { get; }
        public CommitProbe Probe { get; }
        public int SubscriptionWrites => _subscriptions.SessionCreateCalls;
        public List<Guid> BodySessions => _subscriptions.Sessions;
        private readonly CountingSubscriptionRepository _subscriptions;

        private FailureFixture(DisposableMongoReplicaSet mongo, IMongoDatabase database, PlatformDbContext context,
            TenantSubscriptionTransactionWriter writer, CountingSubscriptionRepository subscriptions, Tenant tenant,
            SubscriptionPlan plan, CommitProbe probe, IEnumerable<int> transientQuotaAttempts, bool cancelDuringQuota)
        { _mongo = mongo; Database = database; Context = context; _writer = writer; _subscriptions = subscriptions;
          _tenant = tenant; _plan = plan; Probe = probe; _transientQuotaAttempts = new(transientQuotaAttempts);
          _cancelDuringQuota = cancelDuringQuota; }

        public static async Task<FailureFixture> StartAsync(CommitProbe? probe = null,
            IEnumerable<int>? transientQuotaAttempts = null, bool cancelDuringQuota = false)
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            var tenantId = Guid.NewGuid();
            var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
            var innerSubscriptions = new TenantSubscriptionRepository(context, tenantContext);
            var subscriptions = new CountingSubscriptionRepository(innerSubscriptions);
            var tenants = new TenantRegistryRepository(context, tenantContext);
            var plans = new SubscriptionPlanRepository(context, tenantContext);
            var tenant = new Tenant { Id = tenantId, Code = "FAIL", Slug = "fail", Name = "Failure", DisplayName = "Failure",
                Domain = $"{tenantId:N}.local", Status = TenantStatus.Provisioning };
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Code = "FAIL", Name = "Failure", IsActive = true };
            await database.GetCollection<Tenant>("tenants").InsertOneAsync(tenant);
            await database.GetCollection<SubscriptionPlan>("platform_subscription_plans").InsertOneAsync(plan);
            var user = new Mock<ICurrentUserContext>(); user.SetupGet(x => x.UserId).Returns(Guid.NewGuid()); user.SetupGet(x => x.ActorName).Returns("failure");
            probe ??= new CommitProbe();
            var writer = new TenantSubscriptionTransactionWriter(new PlatformTransactionExecutor(context, probe), subscriptions,
                tenants, plans, new EntitlementStateVersionRepository(context), new MongoIntentWriter(context),
                new MongoAuditWriter(context), user.Object);
            return new FailureFixture(mongo, database, context, writer, subscriptions, tenant, plan, probe,
                transientQuotaAttempts ?? [], cancelDuringQuota);
        }

        public Task<Response<Guid>> CreateAsync() => CreateAsync(CancellationToken.None);
        public Task<Response<Guid>> CreateAsync(CancellationToken ct)
        {
            var subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = _tenant.Id, PlanId = _plan.Id,
                Status = TenantSubscriptionStatus.Active, CurrentPeriodStartUtc = DateTimeOffset.UtcNow,
                CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddMonths(1), UpdatedBy = "failure" };
            return _writer.CreateAsync(subscription, _tenant, _plan, "FailureEvidence", AuditOperation.Create,
                async (session, current, _, participantCt) =>
                {
                    var handle = PlatformMongoTransactionSession.Require(session, Context);
                    var attempt = Interlocked.Increment(ref _quotaAttempt);
                    await Database.GetCollection<BsonDocument>("quota_usages").InsertOneAsync(handle,
                        new BsonDocument("SubscriptionId", current.Id.ToString()), cancellationToken: participantCt);
                    await Database.GetCollection<BsonDocument>("quota_events").InsertOneAsync(handle,
                        new BsonDocument("SubscriptionId", current.Id.ToString()), cancellationToken: participantCt);
                    if (_cancelDuringQuota) throw new OperationCanceledException(participantCt);
                    if (_transientQuotaAttempts.Contains(attempt))
                    { var error = new MongoException("synthetic transient body failure"); error.AddErrorLabel("TransientTransactionError"); throw error; }
                    return Response<NoContent>.Success(204);
                }, ct);
        }

        public async Task AssertParticipantCountsAsync(int expected)
        {
            foreach (var name in new[] { "tenant_subscriptions", EntitlementStateVersionRepository.CollectionName,
                         "quota_usages", "quota_events", "outbox_events", "audit_outbox" })
                Assert.Equal(expected, await Count(name));
            Assert.Equal(1, await Count("tenants"));
            var tenant = await Database.GetCollection<Tenant>("tenants").Find(x => x.Id == _tenant.Id).SingleAsync();
            Assert.Equal(expected == 1 ? _plan.Id : null, tenant.PlanId);
        }
        private Task<long> Count(string name) => Database.GetCollection<BsonDocument>(name)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class CountingSubscriptionRepository(ITenantSubscriptionRepository inner) : ITenantSubscriptionRepository
    {
        public int SessionCreateCalls; public List<Guid> Sessions { get; } = [];
        public Task<TenantSubscription> CreateAsync(IPlatformTransactionSession s, TenantSubscription x, CancellationToken ct = default)
        { SessionCreateCalls++; Sessions.Add(s.TransactionId); return inner.CreateAsync(s, x, ct); }
        public Task<TenantSubscription> CreateAsync(TenantSubscription x, CancellationToken ct = default) => inner.CreateAsync(x, ct);
        public Task<TenantSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<TenantSubscription?> GetByTenantIdAsync(Guid t, Guid id, CancellationToken ct = default) => inner.GetByTenantIdAsync(t, id, ct);
        public Task<TenantSubscription?> GetCurrentByTenantIdAsync(Guid t, CancellationToken ct = default) => inner.GetCurrentByTenantIdAsync(t, ct);
        public Task<IReadOnlyList<TenantSubscription>> GetHistoryByTenantIdAsync(Guid t, CancellationToken ct = default) => inner.GetHistoryByTenantIdAsync(t, ct);
        public Task<bool> HasCurrentAsync(Guid t, Guid? x = null, CancellationToken ct = default) => inner.HasCurrentAsync(t, x, ct);
        public Task UpdateAsync(TenantSubscription x, byte[]? row, CancellationToken ct = default) => inner.UpdateAsync(x, row, ct);
        public Task UpdateAsync(IPlatformTransactionSession s, TenantSubscription x, byte[]? row, CancellationToken ct = default) => inner.UpdateAsync(s, x, row, ct);
    }

    public sealed class CommitProbe(int[]? beforeUnknown = null, int[]? afterUnknown = null,
        int[]? cancelBefore = null, int[]? cancelAfter = null) : IPlatformTransactionFaultProbe
    {
        private readonly HashSet<int> _beforeUnknown = new(beforeUnknown ?? []), _afterUnknown = new(afterUnknown ?? []),
            _cancelBefore = new(cancelBefore ?? []), _cancelAfter = new(cancelAfter ?? []);
        public List<(int Attempt, Guid Session)> CommitAttempts { get; } = [];
        public int AfterDurableUnknownCount { get; private set; }
        public Task BeforeCommitAsync(IPlatformTransactionSession session, int attempt, CancellationToken ct)
        { CommitAttempts.Add((attempt, session.TransactionId)); if (_cancelBefore.Contains(attempt)) throw new OperationCanceledException(ct);
          if (_beforeUnknown.Contains(attempt)) throw Unknown(); return Task.CompletedTask; }
        public Task AfterCommitAsync(IPlatformTransactionSession session, int attempt, CancellationToken ct)
        { if (_cancelAfter.Contains(attempt)) throw new OperationCanceledException(ct); if (_afterUnknown.Contains(attempt))
          { AfterDurableUnknownCount++; throw Unknown(); } return Task.CompletedTask; }
        public void AssertAttempts(params int[] attempts)
        { Assert.Equal(attempts, CommitAttempts.Select(x => x.Attempt)); Assert.Single(CommitAttempts.Select(x => x.Session).Distinct()); }
        private static MongoException Unknown() { var value = new MongoException("synthetic unknown commit");
            value.AddErrorLabel("UnknownTransactionCommitResult"); return value; }
    }

    private sealed class MongoIntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    { public async Task<EventEnvelope<T>> EnqueueAsync<T>(IPlatformTransactionSession session, T value, EventPublishOptions options,
          CancellationToken cancellationToken = default) where T : IIntegrationEvent
      { await context.Database.GetCollection<BsonDocument>("outbox_events").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
            new BsonDocument("EventId", options.EventId!.Value.ToString()), cancellationToken: cancellationToken);
        return new(new(options.EventId.Value, value.EventName, value.EventVersion, options.CorrelationId!.Value, null,
            options.TenantId, options.Producer!, options.OccurredAtUtc!.Value), value); } }
    private sealed class MongoAuditWriter(IPlatformDbContext context) : ITransactionalAuditOutboxWriter
    { public async Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default)
      { await context.Database.GetCollection<BsonDocument>("audit_outbox").InsertOneAsync(PlatformMongoTransactionSession.Require(session, context),
            new BsonDocument("Key", request.IdempotencyKey), cancellationToken: ct); return true; } }
}
