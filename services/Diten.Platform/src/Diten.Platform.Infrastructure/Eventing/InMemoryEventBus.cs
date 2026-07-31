using System.Collections.Concurrent;
using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class InMemoryEventBus : IEventTransportPublisher
{
    private readonly ConcurrentQueue<EventTransportMessage> _messages = new();

    public IReadOnlyCollection<EventTransportMessage> Messages => _messages.ToArray();

    public Func<EventTransportMessage, CancellationToken, Task>? OnPublishAsync { get; set; }

    public async Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);

        if (OnPublishAsync is not null)
        {
            await OnPublishAsync(message, cancellationToken);
        }
    }
}
