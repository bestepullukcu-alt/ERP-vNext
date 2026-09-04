using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Services.Eventing;

public sealed class TransactionalIntegrationEventWriter : ITransactionalIntegrationEventWriter
{
    private readonly ITransactionalOutboxEventWriter _outbox;
    private readonly EventPayloadContractValidator _validator;
    private readonly ITrustedTransportMetadataProvider _transportMetadata;
    private readonly EventBusOptions _options;

    public TransactionalIntegrationEventWriter(ITransactionalOutboxEventWriter outbox,
        EventPayloadContractValidator validator, IOptions<EventBusOptions> options,
        ITrustedTransportMetadataProvider? transportMetadata = null)
    {
        _outbox = outbox;
        _validator = validator;
        _options = options.Value;
        _transportMetadata = transportMetadata ?? new EmptyTrustedTransportMetadataProvider();
    }

    public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session,
        TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        _validator.Validate(@event);
        var metadata = new EventMetadata(options.EventId ?? Guid.NewGuid(), @event.EventName, @event.EventVersion,
            options.CorrelationId ?? Guid.NewGuid(), options.CausationId, options.TenantId,
            string.IsNullOrWhiteSpace(options.Producer) ? _options.Producer : options.Producer,
            options.OccurredAtUtc ?? DateTimeOffset.UtcNow);
        var payload = @event is ICanonicalIntegrationEvent canonical
            ? canonical.CanonicalPayloadUtf8.ToArray()
            : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        if (payload.Length == 0 || payload.Length > _options.MaxCanonicalPayloadBytes)
        {
            throw new EventValidationException($"Event payload must contain 1..{_options.MaxCanonicalPayloadBytes} canonical UTF-8 bytes.");
        }

        var trusted = await _transportMetadata.CreateAsync(metadata, payload, cancellationToken);
        var result = await _outbox.EnqueueAsync(
            session,
            new EventOutboxWriteRequest(metadata, payload, trusted),
            cancellationToken);
        if (result != EventOutboxWriteResult.Inserted)
        {
            throw new InvalidOperationException(
                "Transactional integration-event intent was not inserted exactly once.");
        }

        return new EventEnvelope<TEvent>(metadata, @event);
    }
}
