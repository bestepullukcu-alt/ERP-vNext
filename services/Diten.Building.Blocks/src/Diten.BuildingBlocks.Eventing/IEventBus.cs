namespace Diten.BuildingBlocks.Eventing;

public interface IEventBus
{
    Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
        TEvent @event,
        EventPublishOptions options,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
