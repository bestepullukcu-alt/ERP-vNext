using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Infrastructure.Persistence.Models;

internal sealed class AuditOutboxMessage
{
    private const int MaxRequestTypeLength = 240;
    private const int MaxEntityTypeLength = 160;
    private const int MaxLastErrorLength = 2000;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public AuditOperation Operation { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public Dictionary<string, object?> Payload { get; init; } = [];
    public AuditOutboxStatus Status { get; set; } = AuditOutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? LastError { get; set; }

    public void ValidateForInsert()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("Audit outbox message id is required.");
        }

        if (TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit outbox tenant id is required. Use PlatformSystemTenantId for platform-global audit messages.");
        }

        if (CorrelationId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit outbox correlation id is required.");
        }

        if (string.IsNullOrWhiteSpace(IdempotencyKey))
        {
            throw new InvalidOperationException("Audit outbox idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(RequestType))
        {
            throw new InvalidOperationException("Audit outbox request type is required.");
        }

        if (RequestType.Length > MaxRequestTypeLength)
        {
            throw new InvalidOperationException($"Audit outbox request type cannot exceed {MaxRequestTypeLength} characters.");
        }

        if (Operation == AuditOperation.Unknown)
        {
            throw new InvalidOperationException("Audit outbox operation is required.");
        }

        if (string.IsNullOrWhiteSpace(EntityType))
        {
            throw new InvalidOperationException("Audit outbox entity type is required.");
        }

        if (EntityType.Length > MaxEntityTypeLength)
        {
            throw new InvalidOperationException($"Audit outbox entity type cannot exceed {MaxEntityTypeLength} characters.");
        }

        if (Attempts < 0)
        {
            throw new InvalidOperationException("Audit outbox attempts cannot be negative.");
        }

        if (Payload is null)
        {
            throw new InvalidOperationException("Audit outbox payload is required.");
        }

        if (LastError is { Length: > MaxLastErrorLength })
        {
            throw new InvalidOperationException($"Audit outbox last error cannot exceed {MaxLastErrorLength} characters.");
        }
    }
}
