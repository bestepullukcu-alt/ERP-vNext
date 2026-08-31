using Diten.Platform.Application.Contracts.Eventing;
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
                if (message.TransportHeaders is null)
                {
                    return;
                }

                foreach (var header in message.TransportHeaders)
                {
                    context.Headers.Set(header.Key, header.Value);
                }
            },
            cancellationToken);
    }
}
