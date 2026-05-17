using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Contracts.Eventing;

public sealed class ConsumedEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid EventId { get; init; }

    public string EventName { get; init; } = string.Empty;

    public int EventVersion { get; init; }

    public string ConsumerName { get; init; } = string.Empty;

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public Guid CorrelationId { get; init; }

    public ConsumedEventStatus Status { get; private set; } = ConsumedEventStatus.Started;

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public static ConsumedEvent Started(EventMetadata metadata, string consumerName)
    {
        return new ConsumedEvent
        {
            EventId = metadata.EventId,
            EventName = metadata.EventName,
            EventVersion = metadata.EventVersion,
            ConsumerName = consumerName,
            CorrelationId = metadata.CorrelationId,
            Status = ConsumedEventStatus.Started
        };
    }

    public void MarkConsumed()
    {
        Status = ConsumedEventStatus.Consumed;
        ConsumedAtUtc = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void MarkSkippedDuplicate()
    {
        Status = ConsumedEventStatus.SkippedDuplicate;
        ConsumedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        AttemptCount++;
        Status = ConsumedEventStatus.Failed;
        LastError = EventErrorRedactor.RedactAndTruncate(error);
    }
}

public enum ConsumedEventStatus
{
    Started = 0,
    Consumed = 1,
    SkippedDuplicate = 2,
    Failed = 3
}
