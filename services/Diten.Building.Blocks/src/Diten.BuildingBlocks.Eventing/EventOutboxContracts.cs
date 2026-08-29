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

public enum EventOutboxTerminalFailureKind
{
    Contract = 0,
    Security = 1,
    Validation = 2,
    Unsupported = 3
}

public sealed record EventOutboxTerminalFailure
{
    public EventOutboxTerminalFailure(
        EventOutboxTerminalFailureKind kind,
        string reasonCode,
        string? safeDescription = null)
    {
        if (string.IsNullOrWhiteSpace(reasonCode)
            || reasonCode.Length > 128
            || reasonCode.Any(character =>
                !(character is >= 'a' and <= 'z'
                  or >= '0' and <= '9'
                  or '.'
                  or '-')))
        {
            throw new EventValidationException("Terminal failure reason code is invalid.");
        }

        Kind = kind;
        ReasonCode = reasonCode;
        SafeDescription = safeDescription is null
            ? null
            : EventErrorRedactor.RedactAndTruncate(safeDescription);
    }

    public EventOutboxTerminalFailureKind Kind { get; }
    public string ReasonCode { get; }
    public string? SafeDescription { get; }
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

    Task DeadLetterPublishAsync(
        Guid eventId,
        EventOutboxTerminalFailure failure,
        CancellationToken cancellationToken = default);
}

public sealed class EventOutboxConflictException : Exception
{
    public EventOutboxConflictException(Guid eventId)
        : base($"EventId '{eventId}' already exists with different immutable event content.")
    {
    }
}
