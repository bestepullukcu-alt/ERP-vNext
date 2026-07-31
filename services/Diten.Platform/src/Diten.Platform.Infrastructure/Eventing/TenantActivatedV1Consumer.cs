using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using MassTransit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class TenantActivatedV1Consumer :
    IConsumer<EventTransportMessage>,
    IConsumer<LegacyEventTransportMessage>
{
    private readonly ConsumedEventStore _consumedEventStore;

    public TenantActivatedV1Consumer(ConsumedEventStore consumedEventStore)
    {
        _consumedEventStore = consumedEventStore;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context) =>
        ConsumeMappedAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<LegacyEventTransportMessage> context) =>
        ConsumeMappedAsync(LegacyEventTransportMessageMapper.Map(context.Message), context.CancellationToken);

    private async Task ConsumeMappedAsync(EventTransportMessage message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.EventName, TenantActivatedV1.Name, StringComparison.Ordinal))
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<TenantActivatedV1>(message.PayloadJson)
            ?? throw new InvalidOperationException("Unable to deserialize tenant.activated.v1 payload.");

        var metadata = new EventMetadata(
            message.EventId,
            message.EventName,
            message.EventVersion,
            message.CorrelationId,
            message.CausationId,
            message.TenantId,
            message.Producer,
            message.OccurredAtUtc);

        var envelope = new EventEnvelope<TenantActivatedV1>(metadata, payload);
        await _consumedEventStore.ExecuteOnceAsync(
            envelope,
            nameof(TenantActivatedV1Consumer),
            _ => Task.CompletedTask,
            cancellationToken);
    }
}
