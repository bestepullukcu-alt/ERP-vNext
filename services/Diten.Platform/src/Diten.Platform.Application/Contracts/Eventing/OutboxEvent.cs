using System.Text.Json;
using System.Text;
using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Contracts.Eventing;

public sealed class OutboxEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid EventId { get; init; }

    public string EventName { get; init; } = string.Empty;

    public int EventVersion { get; init; }

    public Guid CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public Guid? TenantId { get; init; }

    public string Producer { get; init; } = string.Empty;

    public string PayloadJson { get; init; } = string.Empty;

    public Dictionary<string, string> TransportHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public OutboxEventStatus Status { get; private set; } = OutboxEventStatus.Pending;

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; private set; }

    public static OutboxEvent FromEnvelope<TEvent>(EventEnvelope<TEvent> envelope, JsonSerializerOptions? serializerOptions = null)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new OutboxEvent
        {
            EventId = envelope.EventId,
            EventName = envelope.EventName,
            EventVersion = envelope.EventVersion,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            TenantId = envelope.TenantId,
            Producer = envelope.Producer,
            OccurredAtUtc = envelope.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(envelope.Payload, serializerOptions)
        };
    }

    public static OutboxEvent FromWriteRequest(EventOutboxWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new OutboxEvent
        {
            EventId = request.Metadata.EventId,
            EventName = request.Metadata.EventName,
            EventVersion = request.Metadata.EventVersion,
            CorrelationId = request.Metadata.CorrelationId,
            CausationId = request.Metadata.CausationId,
            TenantId = request.Metadata.TenantId,
            Producer = request.Metadata.Producer,
            OccurredAtUtc = request.Metadata.OccurredAtUtc,
            PayloadJson = new UTF8Encoding(false, true).GetString(request.CanonicalPayloadUtf8.Span),
            TransportHeaders = new Dictionary<string, string>(
                request.TransportMetadata.Headers,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    public Diten.BuildingBlocks.Eventing.EventTransportMessage ToTransportMessage()
    {
        return new Diten.BuildingBlocks.Eventing.EventTransportMessage(
            EventId,
            EventName,
            EventVersion,
            CorrelationId,
            CausationId,
            TenantId,
            Producer,
            OccurredAtUtc,
            new UTF8Encoding(false, true).GetBytes(PayloadJson),
            new TrustedTransportMetadata(TransportHeaders));
    }

    public bool HasSameImmutableContent(OutboxEvent other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return EventId == other.EventId
               && EventName == other.EventName
               && EventVersion == other.EventVersion
               && CorrelationId == other.CorrelationId
               && CausationId == other.CausationId
               && TenantId == other.TenantId
               && Producer == other.Producer
               && OccurredAtUtc.Equals(other.OccurredAtUtc)
               && PayloadJson == other.PayloadJson
               && TransportHeaders.Count == other.TransportHeaders.Count
               && TransportHeaders.All(pair =>
                   other.TransportHeaders.TryGetValue(pair.Key, out var value)
                   && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    public void MarkPublishing(DateTime? updatedAtUtc = null)
    {
        Status = OutboxEventStatus.Publishing;
        UpdatedAt = updatedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkPublished()
    {
        Status = OutboxEventStatus.Published;
        LastError = null;
        NextAttemptAtUtc = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPublishFailed(string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts)
    {
        AttemptCount++;
        LastError = EventErrorRedactor.RedactAndTruncate(error);
        Status = AttemptCount >= maxAttempts ? OutboxEventStatus.DeadLettered : OutboxEventStatus.Failed;
        NextAttemptAtUtc = AttemptCount >= maxAttempts ? null : nextAttemptAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeadLettered(EventOutboxTerminalFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (Status == OutboxEventStatus.Published)
        {
            throw new InvalidOperationException("A published outbox event cannot be dead-lettered.");
        }

        if (Status == OutboxEventStatus.DeadLettered)
        {
            return;
        }

        AttemptCount++;
        LastError = EventErrorRedactor.RedactAndTruncate(
            $"{failure.Kind}:{failure.ReasonCode}:{failure.SafeDescription}");
        Status = OutboxEventStatus.DeadLettered;
        NextAttemptAtUtc = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum OutboxEventStatus
{
    Pending = 0,
    Publishing = 1,
    Published = 2,
    Failed = 3,
    DeadLettered = 4
}
