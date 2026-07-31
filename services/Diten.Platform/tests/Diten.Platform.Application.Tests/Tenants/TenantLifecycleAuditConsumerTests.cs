using System.Text.Json;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class TenantLifecycleAuditConsumerTests
{
    [Fact]
    public async Task TenantLifecycleAuditConsumer_AppendsOneAuditForEachLifecycleEvent()
    {
        var audit = new CapturingAuditService();
        var consumer = CreateConsumer(audit, new InMemoryConsumedEventRepository());
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        foreach (var message in CreateLifecycleMessages(tenantId, actorId, eventId, correlationId))
        {
            await consumer.ConsumeAsync(message);
        }

        Assert.Equal(7, audit.Requests.Count);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantCreatedV1.Name && request.Operation == AuditOperation.Create);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantActivatedV1.Name && request.Operation == AuditOperation.Activate);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantSuspendedV1.Name && request.Operation == AuditOperation.Suspend);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantReactivatedV1.Name && request.Operation == AuditOperation.Reactivate);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantCancelledV1.Name && request.Operation == AuditOperation.Delete);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantProvisioningCompletedV1.Name && request.Operation == AuditOperation.Execute);
        Assert.Contains(audit.Requests, request => request.RequestType == TenantProvisioningFailedV1.Name && request.Outcome == AuditOutcome.Failed);
    }

    [Fact]
    public async Task TenantLifecycleAuditConsumer_PropagatesCorrelationAndMetadata()
    {
        var audit = new CapturingAuditService();
        var consumer = CreateConsumer(audit, new InMemoryConsumedEventRepository());
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = CreateMessage(
            eventId,
            TenantSuspendedV1.Name,
            TenantSuspendedV1.Version,
            correlationId,
            tenantId,
            occurredAt,
            new TenantSuspendedV1(tenantId, occurredAt, "sensitive reason", actorId));

        await consumer.ConsumeAsync(message);

        var request = Assert.Single(audit.Requests);
        Assert.Equal(correlationId, request.CorrelationId);
        Assert.Equal(actorId, request.ActorId);
        Assert.Equal(tenantId, request.TargetTenantId);
        Assert.Equal(tenantId, request.EntityId);
        Assert.Equal(occurredAt, request.OccurredAtUtc);
        Assert.Equal(eventId, request.Metadata["EventId"]);
        Assert.Equal(TenantSuspendedV1.Name, request.Metadata["EventName"]);
        Assert.Equal(TenantSuspendedV1.Version, request.Metadata["EventVersion"]);
        Assert.Equal(tenantId, request.Metadata["TenantId"]);
        Assert.Equal(correlationId, request.Metadata["CorrelationId"]);
        Assert.Equal(actorId, request.Metadata["ActorId"]);
        Assert.Equal("[REDACTED]", request.Metadata["Reason"]);
    }

    [Fact]
    public async Task TenantLifecycleAuditConsumer_RedactsPiiAndSensitiveData()
    {
        var audit = new CapturingAuditService();
        var consumer = CreateConsumer(audit, new InMemoryConsumedEventRepository());
        var tenantId = Guid.NewGuid();
        var created = CreateMessage(
            Guid.NewGuid(),
            TenantCreatedV1.Name,
            TenantCreatedV1.Version,
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow,
            new TenantCreatedV1(
                tenantId,
                DateTimeOffset.UtcNow,
                null,
                Guid.NewGuid(),
                "Raw Tenant Name",
                "en-US",
                Guid.NewGuid()));
        var failed = CreateMessage(
            Guid.NewGuid(),
            TenantProvisioningFailedV1.Name,
            TenantProvisioningFailedV1.Version,
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow,
            new TenantProvisioningFailedV1(
                tenantId,
                DateTimeOffset.UtcNow,
                "mail",
                "password=plain-secret token=raw-token",
                2));

        await consumer.ConsumeAsync(created);
        await consumer.ConsumeAsync(failed);

        var serialized = JsonSerializer.Serialize(audit.Requests.Select(x => x.Metadata));
        Assert.DoesNotContain("Raw Tenant Name", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-secret", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantLifecycleAuditConsumer_SkipsDuplicateEventIdAndConsumerName()
    {
        var audit = new CapturingAuditService();
        var repository = new InMemoryConsumedEventRepository();
        var consumer = CreateConsumer(audit, repository);
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var message = CreateMessage(
            eventId,
            TenantReactivatedV1.Name,
            TenantReactivatedV1.Version,
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow,
            new TenantReactivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()));

        var first = await consumer.ConsumeAsync(message);
        var duplicate = await consumer.ConsumeAsync(message);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, first);
        Assert.Equal(ConsumedEventExecutionResult.Duplicate, duplicate);
        Assert.Single(audit.Requests);
    }

    [Fact]
    public async Task TenantLifecycleAuditConsumer_IgnoresUnrelatedEvents()
    {
        var audit = new CapturingAuditService();
        var consumer = CreateConsumer(audit, new InMemoryConsumedEventRepository());
        var message = CreateMessage(
            Guid.NewGuid(),
            "unrelated.event.v1",
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new { TenantId = Guid.NewGuid() });

        var result = await consumer.ConsumeAsync(message);

        Assert.Null(result);
        Assert.Empty(audit.Requests);
    }

    [Fact]
    public async Task AuditFailure_DoesNotShareStateWithNotificationMapper()
    {
        var tenantId = Guid.NewGuid();
        var consumer = CreateConsumer(new ThrowingAuditService(), new InMemoryConsumedEventRepository());
        var message = CreateMessage(
            Guid.NewGuid(),
            TenantSuspendedV1.Name,
            TenantSuspendedV1.Version,
            Guid.NewGuid(),
            tenantId,
            DateTimeOffset.UtcNow,
            new TenantSuspendedV1(tenantId, DateTimeOffset.UtcNow, "hold", Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(message)!);

        var mapper = new Application.Features.Tenants.Notifications.TenantSuspendedV1NotificationMapper();
        var envelope = new Diten.BuildingBlocks.Eventing.EventEnvelope<TenantSuspendedV1>(
            new Diten.BuildingBlocks.Eventing.EventMetadata(
                message.EventId,
                message.EventName,
                message.EventVersion,
                message.CorrelationId,
                message.CausationId,
                message.TenantId,
                message.Producer,
                message.OccurredAtUtc),
            new TenantSuspendedV1(tenantId, DateTimeOffset.UtcNow, "hold", Guid.NewGuid()));

        Assert.Null(mapper.Map(envelope));
    }

    private static TenantLifecycleAuditConsumer CreateConsumer(
        IAuditService auditService,
        IConsumedEventRepository repository)
    {
        return new TenantLifecycleAuditConsumer(
            new ConsumedEventStore(repository, NullLogger<ConsumedEventStore>.Instance),
            auditService);
    }

    private static IReadOnlyList<EventTransportMessage> CreateLifecycleMessages(
        Guid tenantId,
        Guid actorId,
        Guid firstEventId,
        Guid correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            CreateMessage(firstEventId, TenantCreatedV1.Name, TenantCreatedV1.Version, correlationId, tenantId, now, new TenantCreatedV1(tenantId, now, Guid.NewGuid(), actorId, "Tenant", "en-US", Guid.NewGuid())),
            CreateMessage(Guid.NewGuid(), TenantActivatedV1.Name, TenantActivatedV1.Version, correlationId, tenantId, now, new TenantActivatedV1(tenantId, now, actorId)),
            CreateMessage(Guid.NewGuid(), TenantSuspendedV1.Name, TenantSuspendedV1.Version, correlationId, tenantId, now, new TenantSuspendedV1(tenantId, now, "hold", actorId)),
            CreateMessage(Guid.NewGuid(), TenantReactivatedV1.Name, TenantReactivatedV1.Version, correlationId, tenantId, now, new TenantReactivatedV1(tenantId, now, actorId)),
            CreateMessage(Guid.NewGuid(), TenantCancelledV1.Name, TenantCancelledV1.Version, correlationId, tenantId, now, new TenantCancelledV1(tenantId, now, now.AddDays(1), "cancel", actorId)),
            CreateMessage(Guid.NewGuid(), TenantProvisioningCompletedV1.Name, TenantProvisioningCompletedV1.Version, correlationId, tenantId, now, new TenantProvisioningCompletedV1(tenantId, now, ["tenant-created"])),
            CreateMessage(Guid.NewGuid(), TenantProvisioningFailedV1.Name, TenantProvisioningFailedV1.Version, correlationId, tenantId, now, new TenantProvisioningFailedV1(tenantId, now, "tenant-created", "redacted failure", 1))
        ];
    }

    private static EventTransportMessage CreateMessage(
        Guid eventId,
        string eventName,
        int eventVersion,
        Guid correlationId,
        Guid tenantId,
        DateTimeOffset occurredAt,
        object payload)
    {
        return new EventTransportMessage(
            eventId,
            eventName,
            eventVersion,
            correlationId,
            Guid.NewGuid(),
            tenantId,
            "Diten.Platform.Tests",
            occurredAt,
            JsonSerializer.Serialize(payload));
    }

    private sealed class CapturingAuditService : IAuditService
    {
        public List<AuditAppendRequest> Requests { get; } = [];

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued(Guid.NewGuid().ToString("N")));
        }
    }

    private sealed class ThrowingAuditService : IAuditService
    {
        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            throw new InvalidOperationException("audit unavailable");
        }
    }

    private sealed class InMemoryConsumedEventRepository : IConsumedEventRepository
    {
        private readonly Dictionary<(Guid EventId, string ConsumerName), ConsumedEvent> _items = [];

        public Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
        {
            var key = (consumedEvent.EventId, consumedEvent.ConsumerName);
            if (!_items.TryGetValue(key, out var existing))
            {
                _items[key] = consumedEvent;
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, consumedEvent));
            }

            if (existing.Status == ConsumedEventStatus.Failed)
            {
                existing.MarkRetryStarted();
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, existing));
            }

            var status = existing.Status == ConsumedEventStatus.Consumed || existing.Status == ConsumedEventStatus.SkippedDuplicate
                ? ConsumedEventStartStatus.ConsumedDuplicate
                : ConsumedEventStartStatus.InFlightDuplicate;
            return Task.FromResult(new ConsumedEventStartResult(status, existing));
        }

        public Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkConsumed();
            return Task.CompletedTask;
        }

        public Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkSkippedDuplicate();
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkFailed(error);
            return Task.CompletedTask;
        }

        public Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue((eventId, consumerName), out var value);
            return Task.FromResult(value);
        }
    }
}
