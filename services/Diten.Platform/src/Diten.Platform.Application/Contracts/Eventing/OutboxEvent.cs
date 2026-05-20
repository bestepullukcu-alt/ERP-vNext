using System.Text.Json;
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

    public EventTransportMessage ToTransportMessage()
    {
        return new EventTransportMessage(
            EventId,
            EventName,
            EventVersion,
            CorrelationId,
            CausationId,
            TenantId,
            Producer,
            OccurredAtUtc,
            PayloadJson);
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
}

public enum OutboxEventStatus
{
    Pending = 0,
    Publishing = 1,
    Published = 2,
    Failed = 3,
    DeadLettered = 4
}
