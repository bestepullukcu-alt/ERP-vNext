using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;

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
    public async Task CanonicalPublish_PersistsExactUtf8AndTrustedMetadataWithoutReserialization()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var canonicalBytes = System.Text.Encoding.UTF8.GetBytes("{\"z\":\"é\",\"a\":1}");
        var provider = new FixedTrustedMetadataProvider(
            new TrustedTransportMetadata(
            [
                new(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256-v1"),
                new(TrustedTransportMetadata.KeyIdHeader, "ppm-review-key"),
                new(TrustedTransportMetadata.SignatureHeader, new string('a', 64))
            ]));
        var bus = new EventBus(
            outbox,
            new EventPayloadContractValidator(),
            Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests", MaxCanonicalPayloadBytes = 128 }),
            NullLogger<EventBus>.Instance,
            provider);

        await bus.PublishAsync(new CanonicalTestEvent(canonicalBytes));

        var stored = Assert.Single(outbox.Items);
        Assert.Equal(canonicalBytes, System.Text.Encoding.UTF8.GetBytes(stored.PayloadJson));
        Assert.Equal("hmac-sha256-v1", stored.TransportHeaders[TrustedTransportMetadata.SignatureSchemeHeader]);
        Assert.Equal("ppm-review-key", stored.TransportHeaders[TrustedTransportMetadata.KeyIdHeader]);
        Assert.Equal(new string('a', 64), stored.TransportHeaders[TrustedTransportMetadata.SignatureHeader]);
    }

    [Fact]
    public async Task SameEventIdAndImmutableContent_IsIdempotent_ButDifferentBytesConflict()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var options = new EventPublishOptions
        {
            EventId = eventId,
            CorrelationId = Guid.NewGuid(),
            OccurredAtUtc = occurredAt
        };
        var bus = CreateEventBus(outbox);

        await bus.PublishAsync(new CanonicalTestEvent(System.Text.Encoding.UTF8.GetBytes("{\"a\":1}")), options);
        await bus.PublishAsync(new CanonicalTestEvent(System.Text.Encoding.UTF8.GetBytes("{\"a\":1}")), options);

        Assert.Single(outbox.Items);
        await Assert.ThrowsAsync<EventOutboxConflictException>(() =>
            bus.PublishAsync(new CanonicalTestEvent(System.Text.Encoding.UTF8.GetBytes("{\"a\":2}")), options));
    }

    [Theory]
    [InlineData("X-Unknown", "value")]
    [InlineData("X-Diten-Event-Signature", "line\r\nbreak")]
    [InlineData(" X-Diten-Event-Signature", "value")]
    public void TrustedMetadata_InvalidNameOrValue_FailsClosed(string name, string value)
    {
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([new(name, value)]));
    }

    [Fact]
    public void TrustedMetadata_DuplicateAndOversizedValues_FailClosed()
    {
        Assert.Throws<EventValidationException>(() =>
            new TrustedTransportMetadata(
            [
                new(TrustedTransportMetadata.SignatureHeader, "a"),
                new(TrustedTransportMetadata.SignatureHeader.ToLowerInvariant(), "b")
            ]));
        Assert.Throws<EventValidationException>(() =>
            new TrustedTransportMetadata(
            [
                new(TrustedTransportMetadata.SignatureHeader, new string('a', TrustedTransportMetadata.MaxHeaderValueBytes + 1))
            ]));
        Assert.Throws<EventValidationException>(() =>
            new TrustedTransportMetadata(
            [
                new(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256-v1")
            ]));
    }

    [Fact]
    public async Task UnsignedEvent_StillPublishesWithNoTrustedHeaders()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));

        Assert.Empty(Assert.Single(outbox.Items).TransportHeaders);
    }

    [Fact]
    public void BusinessPublishOptions_CannotInjectTransportHeaders()
    {
        Assert.DoesNotContain(
            typeof(EventPublishOptions).GetProperties(),
            property => property.Name.Contains("Header", StringComparison.OrdinalIgnoreCase)
                        || typeof(System.Collections.IDictionary).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public async Task CanonicalPayload_EmptyOversizedAndInvalidUtf8_FailClosedBeforePersistence()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = new EventBus(
            outbox,
            new EventPayloadContractValidator(),
            Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests", MaxCanonicalPayloadBytes = 4 }),
            NullLogger<EventBus>.Instance);

        await Assert.ThrowsAsync<EventValidationException>(() => bus.PublishAsync(new CanonicalTestEvent([])));
        await Assert.ThrowsAsync<EventValidationException>(() => bus.PublishAsync(new CanonicalTestEvent([1, 2, 3, 4, 5])));
        await Assert.ThrowsAsync<EventValidationException>(() => bus.PublishAsync(new CanonicalTestEvent([0xc3, 0x28])));
        Assert.Empty(outbox.Items);
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
        Assert.Equal(ConsumedEventStatus.Consumed, consumedEvent!.Status);
    }

    [Fact]
    public async Task OutboxClaim_OnlyOneWorkerCanClaimTheSameEvent()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));

        var firstClaim = await outbox.ClaimNextAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-5));
        var secondClaim = await outbox.ClaimNextAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.NotNull(firstClaim);
        Assert.Equal(OutboxEventStatus.Publishing, firstClaim!.Status);
        Assert.Null(secondClaim);
    }

    [Fact]
    public async Task OutboxClaim_RespectsRetryTime_AndRecoversStalePublishing()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));
        var retryLater = outbox.Items[0];
        var stalePublishing = outbox.Items[1];
        retryLater.MarkPublishFailed("broker unavailable", DateTimeOffset.UtcNow.AddMinutes(5), maxAttempts: 5);
        stalePublishing.MarkPublishing(DateTime.UtcNow.AddMinutes(-10));

        var claim = await outbox.ClaimNextAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-5));
        var retryClaim = await outbox.ClaimNextAsync(DateTimeOffset.UtcNow.AddMinutes(6), DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.NotNull(claim);
        Assert.Equal(stalePublishing.EventId, claim!.EventId);
        Assert.NotNull(retryClaim);
        Assert.Equal(retryLater.EventId, retryClaim!.EventId);
    }

    [Fact]
    public async Task PublishFailure_DeadLettersAfterMaxAttempts()
    {
        var outbox = new InMemoryOutboxEventRepository();
        var bus = CreateEventBus(outbox);
        var processor = CreateProcessor(
            outbox,
            new ThrowingTransportPublisher("broker unavailable"),
            new RabbitMqEventingOptions
            {
                RetryCount = 2,
                InitialRetryDelaySeconds = 0,
                MaxRetryDelaySeconds = 0,
                BatchSize = 1,
                PublishingStaleAfterSeconds = 300
            });

        await bus.PublishAsync(new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null));

        await processor.PublishPendingAsync();
        await processor.PublishPendingAsync();

        var stored = Assert.Single(outbox.Items);
        Assert.Equal(OutboxEventStatus.DeadLettered, stored.Status);
        Assert.Equal(2, stored.AttemptCount);
        Assert.Null(stored.NextAttemptAtUtc);
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

    [Fact]
    public async Task ConsumedFailedEvent_RetryExecutesHandlerAgain()
    {
        var consumed = new InMemoryConsumedEventRepository();
        var consumedStore = new ConsumedEventStore(consumed, NullLogger<ConsumedEventStore>.Instance);
        var envelope = CreateTenantActivatedEnvelope();
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ =>
            {
                attempts++;
                throw new InvalidOperationException("first attempt failed");
            }));
        var retry = await consumedStore.ExecuteOnceAsync(
            envelope,
            "TenantActivatedV1Consumer",
            _ =>
            {
                attempts++;
                return Task.CompletedTask;
            });

        Assert.Equal(ConsumedEventExecutionResult.Consumed, retry);
        Assert.Equal(2, attempts);
        var consumedEvent = await consumed.GetAsync(envelope.EventId, "TenantActivatedV1Consumer");
        Assert.NotNull(consumedEvent);
        Assert.Equal(ConsumedEventStatus.Consumed, consumedEvent!.Status);
    }

    private static EventBus CreateEventBus(IOutboxEventRepository outboxRepository)
    {
        return new EventBus(
            outboxRepository,
            new EventPayloadContractValidator(),
            Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests" }),
            NullLogger<EventBus>.Instance);
    }

    private static OutboxPublisherProcessor CreateProcessor(
        IOutboxEventRepository outboxRepository,
        IEventTransportPublisher transport,
        RabbitMqEventingOptions? options = null)
    {
        return new OutboxPublisherProcessor(
            outboxRepository,
            transport,
            Options.Create(options ?? new RabbitMqEventingOptions
            {
                RetryCount = 5,
                InitialRetryDelaySeconds = 10,
                MaxRetryDelaySeconds = 300,
                BatchSize = 25,
                PublishingStaleAfterSeconds = 300
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

    private sealed class CanonicalTestEvent : ICanonicalIntegrationEvent
    {
        private readonly byte[] _bytes;

        public CanonicalTestEvent(byte[] bytes)
        {
            _bytes = bytes;
        }

        public string EventName => "test.canonical.created.v1";
        public int EventVersion => 1;
        ReadOnlyMemory<byte> ICanonicalIntegrationEvent.CanonicalPayloadUtf8 => _bytes;
    }

    private sealed class FixedTrustedMetadataProvider : ITrustedTransportMetadataProvider
    {
        private readonly TrustedTransportMetadata _metadata;

        public FixedTrustedMetadataProvider(TrustedTransportMetadata metadata)
        {
            _metadata = metadata;
        }

        public ValueTask<TrustedTransportMetadata> CreateAsync(
            EventMetadata metadata,
            ReadOnlyMemory<byte> canonicalPayloadUtf8,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_metadata);
        }
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

        public Task<EventOutboxWriteResult> EnqueueAsync(
            EventOutboxWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            var candidate = OutboxEvent.FromWriteRequest(request);
            var existing = Items.FirstOrDefault(item => item.EventId == candidate.EventId);
            if (existing is null)
            {
                Items.Add(candidate);
                return Task.FromResult(EventOutboxWriteResult.Inserted);
            }

            if (existing.HasSameImmutableContent(candidate))
            {
                return Task.FromResult(EventOutboxWriteResult.Duplicate);
            }

            throw new EventOutboxConflictException(candidate.EventId);
        }

        public Task<IReadOnlyList<OutboxEvent>> GetPendingAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<OutboxEvent> events = Items
                .Where(x => x.Status == OutboxEventStatus.Pending || x.Status == OutboxEventStatus.Failed && x.NextAttemptAtUtc <= nowUtc)
                .Take(batchSize)
                .ToList();
            return Task.FromResult(events);
        }

        public Task<OutboxEvent?> ClaimNextAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset stalePublishingCutoffUtc,
            CancellationToken cancellationToken = default)
        {
            var item = Items
                .Where(x => x.Status == OutboxEventStatus.Pending
                            || x.Status == OutboxEventStatus.Failed && x.NextAttemptAtUtc <= nowUtc
                            || x.Status == OutboxEventStatus.Publishing
                            && x.UpdatedAt is not null
                            && x.UpdatedAt <= stalePublishingCutoffUtc.UtcDateTime)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();
            item?.MarkPublishing();
            return Task.FromResult(item);
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

        public async Task<EventOutboxPublishItem?> ClaimForPublishAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset stalePublishingCutoffUtc,
            CancellationToken cancellationToken = default)
        {
            var item = await ClaimNextAsync(nowUtc, stalePublishingCutoffUtc, cancellationToken);
            if (item is null)
            {
                return null;
            }

            return new EventOutboxPublishItem(
                new EventMetadata(
                    item.EventId,
                    item.EventName,
                    item.EventVersion,
                    item.CorrelationId,
                    item.CausationId,
                    item.TenantId,
                    item.Producer,
                    item.OccurredAtUtc),
                System.Text.Encoding.UTF8.GetBytes(item.PayloadJson),
                new TrustedTransportMetadata(item.TransportHeaders),
                (EventOutboxDeliveryStatus)(int)item.Status,
                item.AttemptCount,
                item.LastError);
        }

        public Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            var item = Items.Single(candidate => candidate.EventId == eventId);
            item.MarkPublished();
            return Task.CompletedTask;
        }

        public Task FailPublishAsync(
            Guid eventId,
            string error,
            DateTimeOffset nextAttemptAtUtc,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            var item = Items.Single(candidate => candidate.EventId == eventId);
            item.MarkPublishFailed(error, nextAttemptAtUtc, maxAttempts);
            return Task.CompletedTask;
        }

        public Task DeadLetterPublishAsync(
            Guid eventId,
            EventOutboxTerminalFailure failure,
            CancellationToken cancellationToken = default)
        {
            Items.Single(candidate => candidate.EventId == eventId).MarkDeadLettered(failure);
            return Task.CompletedTask;
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
