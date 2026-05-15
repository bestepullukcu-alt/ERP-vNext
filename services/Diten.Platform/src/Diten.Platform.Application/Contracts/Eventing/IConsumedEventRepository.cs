namespace Diten.Platform.Application.Contracts.Eventing;

public interface IConsumedEventRepository
{
    Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default);

    Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default);

    Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}

public sealed record ConsumedEventStartResult(bool IsDuplicate, ConsumedEvent Event);
