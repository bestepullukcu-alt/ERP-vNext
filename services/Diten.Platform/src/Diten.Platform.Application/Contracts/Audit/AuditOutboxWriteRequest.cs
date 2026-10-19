using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed class AuditOutboxWriteRequest
{
    private const int MaxRequestTypeLength = 240;
    private const int MaxEntityTypeLength = 160;

    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public AuditOperation Operation { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public IReadOnlyDictionary<string, object?> Payload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// WP-PLATFORM-AUDIT-INTX-01 — what an in-transaction caller says about its change. The in-transaction writer
    /// builds <see cref="Payload"/> from it; the central pipeline (<c>AuditService</c>) leaves it null and supplies the
    /// payload itself, built by the same builder.
    /// </summary>
    public TransactionOwnedAuditIntent? Intent { get; init; }

    public void Validate()
    {
        if (TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit outbox tenant id is required.");
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

        if (Payload is null)
        {
            throw new InvalidOperationException("Audit outbox payload is required.");
        }
    }
}
