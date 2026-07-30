namespace Diten.BuildingBlocks.Eventing;

public sealed record EventOutboxWriteRequest(
    EventMetadata Metadata,
    ReadOnlyMemory<byte> CanonicalPayloadUtf8,
    TrustedTransportMetadata TransportMetadata);

public enum EventOutboxWriteResult
{
    Inserted = 0,
    Duplicate = 1
}

public enum EventOutboxDeliveryStatus
{
    Pending = 0,
    Publishing = 1,
    Published = 2,
    Failed = 3,
    DeadLettered = 4
}

public interface IEventOutboxWriter
{
    Task<EventOutboxWriteResult> EnqueueAsync(
        EventOutboxWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EventOutboxPublishItem(
    EventMetadata Metadata,
    ReadOnlyMemory<byte> CanonicalPayloadUtf8,
    TrustedTransportMetadata TransportMetadata,
    EventOutboxDeliveryStatus Status,
    int AttemptCount,
    string? LastError);

public interface IEventOutboxStore : IEventOutboxWriter
{
    Task<EventOutboxPublishItem?> ClaimForPublishAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset stalePublishingCutoffUtc,
        CancellationToken cancellationToken = default);

    Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task FailPublishAsync(
        Guid eventId,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}

public sealed class EventOutboxConflictException : Exception
{
    public EventOutboxConflictException(Guid eventId)
        : base($"EventId '{eventId}' already exists with different immutable event content.")
    {
    }
}
