using Diten.BuildingBlocks.Eventing;
using MassTransit;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class MassTransitRabbitMqEventPublisher : IEventTransportPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitRabbitMqEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _publishEndpoint.Publish(
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
