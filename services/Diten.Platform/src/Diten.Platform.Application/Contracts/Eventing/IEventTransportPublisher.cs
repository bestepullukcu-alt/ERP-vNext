namespace Diten.Platform.Application.Contracts.Eventing;

public interface IEventTransportPublisher
{
    Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default);
}
