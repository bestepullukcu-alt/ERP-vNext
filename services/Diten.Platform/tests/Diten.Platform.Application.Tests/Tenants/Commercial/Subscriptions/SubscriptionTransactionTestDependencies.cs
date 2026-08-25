using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Subscriptions;

internal sealed class SubscriptionTransactionTestDependencies
{
    public SubscriptionTransactionTestDependencies(ITenantSubscriptionRepository subscriptions,
        ITenantRegistryRepository tenants, ISubscriptionPlanRepository plans, ICurrentUserContext currentUser)
    {
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
