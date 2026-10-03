using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Moq;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Subscriptions;

internal sealed class SubscriptionTransactionTestDependencies
{
    public SubscriptionTransactionTestDependencies(ITenantSubscriptionRepository subscriptions,
        ITenantRegistryRepository tenants, ISubscriptionPlanRepository plans, ICurrentUserContext currentUser)
    {
        // INTX FIX2 item 2 — the writer now reads the live check and the tenant WITH its session. These unit tests have
        // no real session, so a mock's session overload answers what its plain overload was set up to answer.
        Mock.Get(subscriptions).Setup(x => x.HasCurrentAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, Guid tenantId, Guid? exclude, CancellationToken ct) => subscriptions.HasCurrentAsync(tenantId, exclude, ct));
        Mock.Get(tenants).Setup(x => x.GetByIdAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, Guid id, CancellationToken ct) => tenants.GetByIdAsync(id, ct));
        // INTX FIX3 — an update's before-state and its plan are read in the transaction too.
        Mock.Get(subscriptions).Setup(x => x.GetByIdAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((IPlatformTransactionSession _, Guid id, CancellationToken ct) => subscriptions.GetByIdAsync(id, ct));
        plans = SessionlessPlanReads.Over(plans);

        Events = new CapturingEventWriter();
        Writer = new TenantSubscriptionTransactionWriter(new InlineExecutor(), subscriptions, tenants, plans,
            new VersionRepository(), Events, new AuditWriter(), currentUser);
    }

    public TenantSubscriptionTransactionWriter Writer { get; }
    public CapturingEventWriter Events { get; }

    private sealed class TestSession : IPlatformTransactionSession { public Guid TransactionId { get; } = Guid.NewGuid(); }
    private sealed class InlineExecutor : IPlatformTransactionExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
            CancellationToken cancellationToken = default) => body(new TestSession(), cancellationToken);
    }
    private sealed class VersionRepository : IEntitlementStateVersionRepository
    {
        public Task<ulong> IncrementPhysicalEntitlementVersionAsync(IPlatformTransactionSession session, Guid tenantId, string moduleCode, CancellationToken cancellationToken = default) => Task.FromResult(1UL);
        public Task<ulong> IncrementSubscriptionSelectionVersionAsync(IPlatformTransactionSession session, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(1UL);
        public Task<ulong> IncrementGlobalApplicabilityVersionAsync(IPlatformTransactionSession session, CancellationToken cancellationToken = default) => Task.FromResult(1UL);
    }
    private sealed class AuditWriter : ITransactionalAuditOutboxWriter
    {
        public Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) => Task.FromResult(true);
    }
}

/// <summary>
/// INTX FIX3 — the writer reads the plan in its transaction (ITransactionalSubscriptionPlanRepository). A test that mocks
/// only <see cref="ISubscriptionPlanRepository"/> answers that read with what its plain read was set up to return;
/// nothing else is done in a transaction through it.
/// </summary>
internal sealed class SessionlessPlanReads(ISubscriptionPlanRepository inner) : ITransactionalSubscriptionPlanRepository
{
    public static ISubscriptionPlanRepository Over(ISubscriptionPlanRepository plans) =>
        plans as ITransactionalSubscriptionPlanRepository ?? new SessionlessPlanReads(plans);

    public Task<SubscriptionPlan?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
    public Task<SubscriptionPlan> CreateAsync(IPlatformTransactionSession session, SubscriptionPlan plan, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionPlan?> GetByCodeAsync(IPlatformTransactionSession session, string code, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsByCodeAsync(IPlatformTransactionSession session, string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionPlan?> GetActiveDefaultAsync(IPlatformTransactionSession session, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateAsync(IPlatformTransactionSession session, SubscriptionPlan plan, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionPlan> CreateAsync(SubscriptionPlan plan, CancellationToken ct = default) => inner.CreateAsync(plan, ct);
    public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
    public Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default) => inner.GetByCodeAsync(code, ct);
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
    public Task<SubscriptionPlan?> GetActiveDefaultAsync(Guid? excludeId = null, CancellationToken ct = default) => inner.GetActiveDefaultAsync(excludeId, ct);
    public Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default) => inner.UpdateAsync(plan, ct);
    public Task<(IReadOnlyList<SubscriptionPlan> Items, long TotalCount)> QueryAsync(SubscriptionPlansQuery query, CancellationToken ct = default) => inner.QueryAsync(query, ct);
    public Task<IReadOnlyList<SubscriptionPlan>> GetActiveAsync(CancellationToken ct = default) => inner.GetActiveAsync(ct);
    public Task<IReadOnlyList<SubscriptionPlan>> GetByIncludedModuleKeyAsync(string moduleKey, CancellationToken ct = default) => inner.GetByIncludedModuleKeyAsync(moduleKey, ct);
    public Task<SubscriptionPlanSummary> GetSummaryAsync(CancellationToken ct = default) => inner.GetSummaryAsync(ct);
}

internal sealed class CapturingEventWriter : ITransactionalIntegrationEventWriter
{
    public object? Event { get; private set; }
    public EventPublishOptions? Options { get; private set; }
    public int Count { get; private set; }

    public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
        EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
    {
        Event = @event;
        Options = options;
        Count++;
        return Task.FromResult(new EventEnvelope<TEvent>(new EventMetadata(
            options.EventId ?? Guid.NewGuid(), @event.EventName, @event.EventVersion,
            options.CorrelationId ?? Guid.NewGuid(), options.CausationId, options.TenantId,
            options.Producer ?? "Diten.Platform", options.OccurredAtUtc ?? DateTimeOffset.UtcNow), @event));
    }
}
