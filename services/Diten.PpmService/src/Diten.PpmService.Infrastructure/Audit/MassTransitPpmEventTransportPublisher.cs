using Diten.BuildingBlocks.Eventing;
using MassTransit;

namespace Diten.PpmService.Infrastructure.Audit;

public sealed class MassTransitPpmEventTransportPublisher(IPublishEndpoint publishEndpoint)
    : IEventTransportPublisher
{
    public Task PublishAsync(
        EventTransportMessage message,
        CancellationToken cancellationToken = default)
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
