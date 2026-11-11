using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed record AuditAppendRequest
{
    public Guid CorrelationId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public AuditActorType ActorType { get; init; } = AuditActorType.System;
    public Guid? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorDisplayName { get; init; }
    public Guid? TargetTenantId { get; init; }
    public AuditCategory Category { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public AuditOperation Operation { get; init; }
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Succeeded;
    public IReadOnlyDictionary<string, object?>? BeforeState { get; init; }
    public IReadOnlyDictionary<string, object?>? AfterState { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
    public string SourceService { get; init; } = "Diten.Platform";
    public string? SourceModule { get; init; }
    public bool IsMetaAudit { get; init; }
    public bool IsPlatformGlobal { get; init; }
    public int Sequence { get; init; }

    /// <summary>
    /// BL-501 — the sender's own id for this event (the shared central-audit forwarder's outbox row id). When present, the
    /// event is filed ONCE per id: a resend after a lost acknowledgement answers Duplicate, and two different events that
    /// happen to share correlation / type / entity / operation stay two records. Absent → the composite key, as before.
    /// </summary>
    public Guid? EventId { get; init; }
}
