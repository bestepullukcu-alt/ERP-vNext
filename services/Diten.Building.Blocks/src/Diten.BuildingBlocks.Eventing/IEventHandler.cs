namespace Diten.BuildingBlocks.Eventing;

public interface IEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default);
}
