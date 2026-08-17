using Diten.AuthService.Application.Common.Events;

namespace Diten.AuthService.Application.Common.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public EventEnvelope Envelope { get; init; } = default!;
    public OutboxStatus Status { get; private set; } = OutboxStatus.Pending;
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextRetryAt { get; private set; }

    public void MarkPublished() => Status = OutboxStatus.Published;

    public void MarkRetry(TimeSpan delay)
    {
        RetryCount++;
        NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
        Status = RetryCount >= 10 ? OutboxStatus.Failed : OutboxStatus.Pending;
    }
}

public enum OutboxStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}
