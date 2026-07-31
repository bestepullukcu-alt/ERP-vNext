namespace Diten.BuildingBlocks.Eventing;

public interface IEventTransportPublisher
{
    Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default);
}
