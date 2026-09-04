using System.Text;
using System.Text.Json;

namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// Reusable public event-bus implementation. Domain services supply their own
/// transaction-aware <see cref="IEventOutboxWriter"/> persistence adapter.
/// </summary>
public sealed class OutboxEventBus : IEventBus
{
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly EventPayloadContractValidator _payloadValidator;
    private readonly ITrustedTransportMetadataProvider _transportMetadataProvider;
    private readonly string _producer;
    private readonly int _maxCanonicalPayloadBytes;

    public OutboxEventBus(
        IEventOutboxWriter outboxWriter,
        EventPayloadContractValidator payloadValidator,
        ITrustedTransportMetadataProvider transportMetadataProvider,
        string producer,
        int maxCanonicalPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(outboxWriter);
        ArgumentNullException.ThrowIfNull(payloadValidator);
        ArgumentNullException.ThrowIfNull(transportMetadataProvider);
        if (string.IsNullOrWhiteSpace(producer))
        {
            throw new ArgumentException("A non-empty producer is required.", nameof(producer));
        }

        if (maxCanonicalPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCanonicalPayloadBytes));
        }

        _outboxWriter = outboxWriter;
        _payloadValidator = payloadValidator;
        _transportMetadataProvider = transportMetadataProvider;
        _producer = producer;
        _maxCanonicalPayloadBytes = maxCanonicalPayloadBytes;
    }

    public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
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
            string.IsNullOrWhiteSpace(options.Producer) ? _producer : options.Producer,
            options.OccurredAtUtc ?? DateTimeOffset.UtcNow);
        var payloadUtf8 = @event is ICanonicalIntegrationEvent canonicalEvent
            ? canonicalEvent.CanonicalPayloadUtf8.ToArray()
            : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

        if (payloadUtf8.Length == 0 || payloadUtf8.Length > _maxCanonicalPayloadBytes)
        {
            throw new EventValidationException(
                $"Event payload must contain 1..{_maxCanonicalPayloadBytes} canonical UTF-8 bytes.");
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(payloadUtf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw new EventValidationException($"Canonical event payload is not valid UTF-8: {exception.Message}");
        }

        var transportMetadata = await _transportMetadataProvider.CreateAsync(
            metadata,
            payloadUtf8,
            cancellationToken);
        ArgumentNullException.ThrowIfNull(transportMetadata);

        await _outboxWriter.EnqueueAsync(
            new EventOutboxWriteRequest(metadata, payloadUtf8, transportMetadata),
            cancellationToken);

        return new EventEnvelope<TEvent>(metadata, @event);
    }
}
