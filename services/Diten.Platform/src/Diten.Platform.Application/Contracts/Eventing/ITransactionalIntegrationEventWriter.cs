using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Contracts.Eventing;

public interface ITransactionalIntegrationEventWriter
{
    Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
        EventPublishOptions options, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
