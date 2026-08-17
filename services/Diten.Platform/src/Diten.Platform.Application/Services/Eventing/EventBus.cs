using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Services.Eventing;

public sealed class EventBus : IEventBus
{
    private readonly IOutboxEventRepository _outboxRepository;
    private readonly EventPayloadContractValidator _payloadValidator;
    private readonly EventBusOptions _options;
    private readonly ILogger<EventBus> _logger;

    public EventBus(
        IOutboxEventRepository outboxRepository,
        EventPayloadContractValidator payloadValidator,
        IOptions<EventBusOptions> options,
        ILogger<EventBus> logger)
    {
        _outboxRepository = outboxRepository;
        _payloadValidator = payloadValidator;
        _options = options.Value;
        _logger = logger;
    }

    public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        return PublishAsync(@event, new EventPublishOptions(), cancellationToken);
    }

    public async Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
        TEvent @event,
        EventPublishOptions options,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(options);

        _payloadValidator.Validate(@event);

        var metadata = new EventMetadata(
            options.EventId ?? Guid.NewGuid(),
            @event.EventName,
            @event.EventVersion,
            options.CorrelationId ?? Guid.NewGuid(),
            options.CausationId,
            options.TenantId,
            string.IsNullOrWhiteSpace(options.Producer) ? _options.Producer : options.Producer,
            options.OccurredAtUtc ?? DateTimeOffset.UtcNow);

        var envelope = new EventEnvelope<TEvent>(metadata, @event);
        await _outboxRepository.AddAsync(OutboxEvent.FromEnvelope(envelope), cancellationToken);

        _logger.LogInformation(
            "event.outbox.created EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
            envelope.EventId,
            envelope.EventName,
            envelope.EventVersion,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.TenantId,
            envelope.Producer,
            OutboxEventStatus.Pending,
            0,
            envelope.OccurredAtUtc);

        return envelope;
    }
}
