using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class EventingMvpTests
{
    [Fact]
    public void EventName_RequiresVersionSuffix()
    {
        Assert.True(EventName.IsValid("tenant.activated.v1"));
        Assert.False(EventName.IsValid("tenant.activated"));
        Assert.False(EventName.IsValid("Tenant.Activated.V1"));
    }

    [Fact]
    public void EventName_VersionMustMatchEventVersion()
    {
        EventName.EnsureMatchesVersion("tenant.activated.v1", 1);

        Assert.Throws<EventValidationException>(() => EventName.EnsureMatchesVersion("tenant.activated.v1", 2));
    }

    [Fact]
    public void ContractValidation_RejectsForbiddenPayloadShapes()
    {
        var validator = new EventPayloadContractValidator();

        Assert.Throws<EventValidationException>(() => validator.Validate(new PayloadWithEntity(Guid.NewGuid(), new BaseEntity())));
        Assert.Throws<EventValidationException>(() => validator.Validate(new PayloadWithCollection(Guid.NewGuid(), [Guid.NewGuid()])));
        Assert.Throws<EventValidationException>(() => validator.Validate(new PayloadWithBinary(Guid.NewGuid(), [1, 2, 3])));
        Assert.Throws<EventValidationException>(() => validator.Validate(new PayloadWithSecret(Guid.NewGuid(), "token-value")));
    }

    [Fact]
    public async Task PublishAsync_CreatesOutbox_WithoutCallingRabbitMq()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var envelope = await bus.PublishAsync(
            new TenantActivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()),
            new EventPublishOptions
            {
                CorrelationId = correlationId,
                CausationId = causationId,
                TenantId = tenantId,
                Producer = "Diten.Platform.Tests"
            });

        var stored = Assert.Single(outbox.Items);
        Assert.Equal(TenantActivatedV1.Name, stored.EventName);
        Assert.Equal(TenantActivatedV1.Version, stored.EventVersion);
        Assert.Equal(envelope.EventId, stored.EventId);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Equal(causationId, stored.CausationId);
        Assert.Equal(OutboxEventStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task GoldenFlow_PublishesFromOutbox_AndSkipsDuplicateConsumerDelivery()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var consumed = new InMemoryConsumedEventRepository();
        var bus = CreateEventBus(outbox);
        var transport = new InMemoryEventBus();
        var processor = CreateProcessor(outbox, transport);
        var consumedStore = new ConsumedEventStore(consumed, NullLogger<ConsumedEventStore>.Instance);

        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        await bus.PublishAsync(
            new TenantActivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()),
            new EventPublishOptions
            {
                CorrelationId = correlationId,
                CausationId = causationId,
                TenantId = tenantId,
                Producer = "Diten.Platform.Tests"
            });

        var publishedCount = await processor.PublishPendingAsync();
        var message = Assert.Single(transport.Messages);
        Assert.Equal(1, publishedCount);
        Assert.Equal(TenantActivatedV1.Name, message.EventName);
        Assert.Equal(correlationId, message.CorrelationId);
        Assert.Equal(causationId, message.CausationId);

        var payload = JsonSerializer.Deserialize<TenantActivatedV1>(message.PayloadJson)!;
        var envelope = new EventEnvelope<TenantActivatedV1>(
            new EventMetadata(
                message.EventId,
                message.EventName,
                message.EventVersion,
                message.CorrelationId,
                message.CausationId,
                message.TenantId,
                message.Producer,
                message.OccurredAtUtc),
            payload);

        var sideEffects = 0;
        var first = await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ =>
            {
                sideEffects++;
                return Task.CompletedTask;
            });
        var duplicate = await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ =>
            {
                sideEffects++;
                return Task.CompletedTask;
            });

        Assert.Equal(ConsumedEventExecutionResult.Consumed, first);
        Assert.Equal(ConsumedEventExecutionResult.Duplicate, duplicate);
        Assert.Equal(1, sideEffects);

        var consumedEvent = await consumed.GetAsync(message.EventId, "TenantActivatedV1Consumer");
        Assert.NotNull(consumedEvent);
        Assert.Equal(ConsumedEventStatus.SkippedDuplicate, consumedEvent!.Status);
    }

    [Fact]
    public async Task ConsumeObservabilitySink_ReceivesSucceededDuplicateAndFailedCallbacks()
    {
        var consumed = new InMemoryConsumedEventRepository();
        var sink = new RecordingEventingObservabilitySink();
        var consumedStore = new ConsumedEventStore(consumed, NullLogger<ConsumedEventStore>.Instance, [sink]);
        var envelope = CreateTenantActivatedEnvelope();

        await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ => Task.CompletedTask);
        await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ => Task.CompletedTask);

        var failedEnvelope = CreateTenantActivatedEnvelope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumedStore.ExecuteOnceAsync(
            failedEnvelope,
            "TenantActivatedV1Consumer",
            _ => throw new InvalidOperationException("test failure")));

        Assert.Contains(sink.Entries, entry => entry.EventName == TenantActivatedV1.Name
                                               && entry.Result == "succeeded"
                                               && entry.CorrelationId == envelope.CorrelationId.ToString());
        Assert.Contains(sink.Entries, entry => entry.EventName == TenantActivatedV1.Name
                                               && entry.Result == "duplicate"
                                               && entry.CorrelationId == envelope.CorrelationId.ToString());
        Assert.Contains(sink.Entries, entry => entry.EventName == TenantActivatedV1.Name
                                               && entry.Result == "failed"
                                               && entry.CorrelationId == failedEnvelope.CorrelationId.ToString());
    }

    [Fact]
    public async Task ConsumeObservabilitySinkFailure_DoesNotBreakConsumption()
    {
        var consumed = new InMemoryConsumedEventRepository();
        var consumedStore = new ConsumedEventStore(
            consumed,
            NullLogger<ConsumedEventStore>.Instance,
            [new ThrowingEventingObservabilitySink()]);
        var envelope = CreateTenantActivatedEnvelope();

        var result = await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ => Task.CompletedTask);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        var consumedEvent = await consumed.GetAsync(envelope.EventId, "TenantActivatedV1Consumer");
        Assert.NotNull(consumedEvent);
        Assert.Equal(ConsumedEventStatus.Consumed, consumedEvent!.Status);
    }

    [Fact]
    public async Task OutboxObservabilityReader_ReturnsPendingCountOnly()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
        outbox.Items[0].MarkPublishing();
        outbox.Items[0].MarkPublished();

        var count = await outbox.GetPendingCountAsync();

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RabbitMqUnavailable_KeepsOutboxRetryableState()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);
        var processor = CreateProcessor(outbox, new ThrowingTransportPublisher("connectionString=secret; token=abc"));

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));

        var published = await processor.PublishPendingAsync();

        var stored = Assert.Single(outbox.Items);
        Assert.Equal(0, published);
        Assert.Equal(OutboxEventStatus.Failed, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.NotNull(stored.NextAttemptAtUtc);
        Assert.NotNull(stored.LastError);
        Assert.DoesNotContain("secret", stored.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc", stored.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.True(stored.LastError!.Length <= 4000);
    }

    private static EventBus CreateEventBus(IOutboxEventRepository outboxRepository)
    {
        return new EventBus(
            outboxRepository,
            new EventPayloadContractValidator(),
            Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests" }),
            NullLogger<EventBus>.Instance);
    }

    private static OutboxPublisherProcessor CreateProcessor(IOutboxEventRepository outboxRepository, IEventTransportPublisher transport)
    {
        return new OutboxPublisherProcessor(
            outboxRepository,
            transport,
            Options.Create(new RabbitMqEventingOptions
            {
                RetryCount = 5,
                InitialRetryDelaySeconds = 10,
                MaxRetryDelaySeconds = 300,
                BatchSize = 25
            }),
            NullLogger<OutboxPublisherProcessor>.Instance);
    }

    private static EventEnvelope<TenantActivatedV1> CreateTenantActivatedEnvelope()
    {
        var tenantId = Guid.NewGuid();
        return new EventEnvelope<TenantActivatedV1>(
            new EventMetadata(
                Guid.NewGuid(),
                TenantActivatedV1.Name,
                TenantActivatedV1.Version,
                Guid.NewGuid(),
                null,
                tenantId,
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow),
            new TenantActivatedV1(tenantId, DateTimeOffset.UtcNow, null));
    }

    private sealed record PayloadWithEntity(Guid Id, BaseEntity Entity) : IIntegrationEvent
    {
        public string EventName => "test.entity.created.v1";
        public int EventVersion => 1;
    }

    private sealed record PayloadWithCollection(Guid Id, IReadOnlyCollection<Guid> Items) : IIntegrationEvent
    {
        public string EventName => "test.collection.created.v1";
        public int EventVersion => 1;
    }

    private sealed record PayloadWithBinary(Guid Id, byte[] Blob) : IIntegrationEvent
    {
        public string EventName => "test.binary.created.v1";
        public int EventVersion => 1;
    }

    private sealed record PayloadWithSecret(Guid Id, string AccessToken) : IIntegrationEvent
    {
        public string EventName => "test.secret.created.v1";
        public int EventVersion => 1;
    }

    private sealed class BaseEntity
    {
        public Guid Id { get; init; }
    }

    private sealed class ThrowingTransportPublisher : IEventTransportPublisher
    {
        private readonly string _message;

        public ThrowingTransportPublisher(string message)
        {
            _message = message;
        }

        public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class InMemoryOutboxEventRepository : IOutboxEventRepository, IOutboxObservabilityReader
    {
        public List<OutboxEvent> Items { get; } = [];

        public Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
        {
            Items.Add(outboxEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxEvent>> GetPendingAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<OutboxEvent> events = Items
                .Where(x => x.Status == OutboxEventStatus.Pending || x.Status == OutboxEventStatus.Failed && x.NextAttemptAtUtc <= nowUtc)
                .Take(batchSize)
                .ToList();
            return Task.FromResult(events);
        }

        public Task UpdateAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<OutboxEvent?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(x => x.EventId == eventId));
        }

        public Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var count = Items.LongCount(x => x.Status == OutboxEventStatus.Pending
                                            || x.Status == OutboxEventStatus.Failed && x.NextAttemptAtUtc <= nowUtc);
            return Task.FromResult(count);
        }
    }

    private sealed class InMemoryConsumedEventRepository : IConsumedEventRepository
    {
        private readonly Dictionary<(Guid EventId, string ConsumerName), ConsumedEvent> _items = [];

        public Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
        {
            var key = (consumedEvent.EventId, consumedEvent.ConsumerName);
            if (_items.TryGetValue(key, out var existing))
            {
                return Task.FromResult(new ConsumedEventStartResult(true, existing));
            }

            _items[key] = consumedEvent;
            return Task.FromResult(new ConsumedEventStartResult(false, consumedEvent));
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

    private sealed class RecordingEventingObservabilitySink : IEventingObservabilitySink
    {
        public List<Entry> Entries { get; } = [];

        public Task OnEventConsumedAsync(
            string eventName,
            string eventVersion,
            string? consumerName,
            string result,
            TimeSpan duration,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(new Entry(eventName, eventVersion, consumerName, result, duration, correlationId));
            return Task.CompletedTask;
        }

        public sealed record Entry(
            string EventName,
            string EventVersion,
            string? ConsumerName,
            string Result,
            TimeSpan Duration,
            string? CorrelationId);
    }

    private sealed class ThrowingEventingObservabilitySink : IEventingObservabilitySink
    {
        public Task OnEventConsumedAsync(
            string eventName,
            string eventVersion,
            string? consumerName,
            string result,
            TimeSpan duration,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("observer failed");
        }
    }
}
