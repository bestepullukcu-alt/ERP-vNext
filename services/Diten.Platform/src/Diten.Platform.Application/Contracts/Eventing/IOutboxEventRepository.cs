namespace Diten.Platform.Application.Contracts.Eventing;

public interface IOutboxEventRepository
{
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxEvent>> GetPendingAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken = default);

    Task<OutboxEvent?> ClaimNextAsync(DateTimeOffset nowUtc, DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default);

    Task UpdateAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default);

    Task<OutboxEvent?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
