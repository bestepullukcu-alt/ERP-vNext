using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Audit;

public sealed class AuditEvent : TenantScopedEntity
{
    private const int MaxEntityTypeLength = 160;
    private const int MaxRequestTypeLength = 240;
    private const int MaxSourceLength = 120;
    private const int MaxRedactionReasonLength = 500;

    [SetsRequiredMembers]
    public AuditEvent()
    {
        TenantId = Guid.Empty;
    }

    public Guid CorrelationId { get; init; }
    public string? RequestType { get; init; }
    public AuditActorType ActorType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorEmailMasked { get; init; }
    public string? ActorDisplayNameMasked { get; init; }
    public Guid? TargetTenantId { get; init; }
    public AuditCategory Category { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public AuditOperation Operation { get; init; }
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Succeeded;
    public Dictionary<string, object?>? BeforeState { get; init; }
    public Dictionary<string, object?>? AfterState { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = [];
    public string? IpAddressMasked { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset WrittenAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string SourceService { get; init; } = "Diten.Platform";
    public string? SourceModule { get; init; }
    public bool IsMetaAudit { get; init; }
    public AuditRedactionStatus RedactionStatus { get; init; } = AuditRedactionStatus.None;
    public DateTimeOffset? RedactedAtUtc { get; init; }
    public Guid? RedactedByActorId { get; init; }
    public string? RedactionReason { get; init; }

    public void ValidateAppend()
    {
        if (TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit event tenant id is required. Use PlatformSystemTenantId for platform-global audit events.");
        }

        if (IsDeleted)
        {
            throw new InvalidOperationException("Audit events are append-only and cannot be inserted as deleted.");
        }

        if (UpdatedAt.HasValue || !string.IsNullOrWhiteSpace(UpdatedBy))
        {
            throw new InvalidOperationException("Audit events are immutable and cannot be inserted with update metadata.");
        }

        if (CorrelationId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit event correlation id is required.");
        }

        if (Category == AuditCategory.Unknown)
        {
            throw new InvalidOperationException("Audit event category is required.");
        }

        if (Operation == AuditOperation.Unknown)
        {
            throw new InvalidOperationException("Audit event operation is required.");
        }

        if (Outcome == AuditOutcome.Unknown)
        {
            throw new InvalidOperationException("Audit event outcome is required.");
        }

        if (string.IsNullOrWhiteSpace(EntityType))
        {
            throw new InvalidOperationException("Audit event entity type is required.");
        }

        if (EntityType.Length > MaxEntityTypeLength)
        {
            throw new InvalidOperationException($"Audit event entity type cannot exceed {MaxEntityTypeLength} characters.");
        }

        if (RequestType is { Length: > MaxRequestTypeLength })
        {
            throw new InvalidOperationException($"Audit event request type cannot exceed {MaxRequestTypeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(SourceService))
        {
            throw new InvalidOperationException("Audit event source service is required.");
        }

        if (SourceService.Length > MaxSourceLength)
        {
            throw new InvalidOperationException($"Audit event source service cannot exceed {MaxSourceLength} characters.");
        }

        if (RedactionStatus is AuditRedactionStatus.None or AuditRedactionStatus.SensitiveFieldsRedacted)
        {
            return;
        }

        if (!RedactedAtUtc.HasValue || RedactedByActorId is null || RedactedByActorId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit event redaction metadata is required when redaction status is set.");
        }

        if (RedactionReason is { Length: > MaxRedactionReasonLength })
        {
            throw new InvalidOperationException($"Audit event redaction reason cannot exceed {MaxRedactionReasonLength} characters.");
        }
    }
}
