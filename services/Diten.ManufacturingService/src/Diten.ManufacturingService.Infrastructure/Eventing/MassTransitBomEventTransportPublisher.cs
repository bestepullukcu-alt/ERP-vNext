using Diten.BuildingBlocks.Eventing;
using MassTransit;

namespace Diten.ManufacturingService.Infrastructure.Eventing;

/// <summary>
/// Broker transport for the shared outbox processor — the repository's existing MassTransit + RabbitMQ pattern
/// (<c>MassTransitPpmEventTransportPublisher</c>). The message is the shared <see cref="EventTransportMessage"/>;
/// its <c>CanonicalPayloadUtf8</c> is the contract <c>BomVersionEvent</c>.
/// </summary>
public sealed class MassTransitBomEventTransportPublisher(IPublishEndpoint publishEndpoint) : IEventTransportPublisher
{
    public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return publishEndpoint.Publish(
            message,
            context =>
            {
                foreach (var header in message.TransportMetadata.Headers)
                {
                    context.Headers.Set(header.Key, header.Value);
                }
            },
            cancellationToken);
    }
}
