using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Models;

namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed record AuditOutboxProcessingItem(
    Guid Id,
    Guid TenantId,
    Guid CorrelationId,
    string IdempotencyKey,
    string RequestType,
    AuditOperation Operation,
    string EntityType,
    Guid? EntityId,
    IReadOnlyDictionary<string, object?> Payload,
    AuditOutboxStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset CreatedAtUtc);
