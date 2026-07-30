using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Services.Eventing;

public sealed class EventBus : IEventBus
{
    private readonly OutboxEventBus _inner;
    private readonly ILogger<EventBus> _logger;

    public EventBus(
        IOutboxEventRepository outboxRepository,
        EventPayloadContractValidator payloadValidator,
        IOptions<EventBusOptions> options,
        ILogger<EventBus> logger,
        ITrustedTransportMetadataProvider? transportMetadataProvider = null)
    {
        _logger = logger;
        var eventBusOptions = options.Value;
        _inner = new OutboxEventBus(
            outboxRepository,
            payloadValidator,
            transportMetadataProvider ?? new EmptyTrustedTransportMetadataProvider(),
            eventBusOptions.Producer,
            eventBusOptions.MaxCanonicalPayloadBytes);
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
        var envelope = await _inner.PublishAsync(@event, options, cancellationToken);

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
