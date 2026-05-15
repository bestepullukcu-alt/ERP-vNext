using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Repositories;

public sealed record AuditEventSearchRequest(
    Guid? TenantId,
    Guid? TargetTenantId,
    Guid? CorrelationId,
    Guid? ActorId,
    AuditCategory? Category,
    AuditOperation? Operation,
    AuditOutcome? Outcome,
    string? EntityType,
    Guid? EntityId,
    string? SourceModule,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Skip,
    int Take);

public sealed record AuditEventSearchResult(
    IReadOnlyList<AuditEvent> Items,
    long TotalCount);

public sealed record AuditActorPiiRedactionRequest(
    Guid ActorId,
    DateTimeOffset RedactedAtUtc,
    Guid RedactedByActorId,
    string Reason);
