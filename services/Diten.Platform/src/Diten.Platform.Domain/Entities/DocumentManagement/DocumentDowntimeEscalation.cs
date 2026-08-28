using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU20 — an escalation raised against a repository downtime event (GMG-QMS-SOP-0001 §11.3): the outage
/// has run beyond 2 working days, a temporary issue has missed its 3-working-day reconciliation, or a BCP
/// assessment is required.
///
/// Escalations are IDEMPOTENT per (event, type, role): re-evaluating an ongoing outage re-uses the open escalation
/// rather than stacking duplicates, so the escalation list stays a truthful register of distinct obligations.
/// They are never hard-deleted — resolution and closure are status changes, because the fact that an escalation
/// was ever raised is itself the audit evidence.
/// </summary>
public sealed class DocumentDowntimeEscalation : TenantScopedEntity
{
    public required Guid DowntimeEventId { get; set; }

    /// <summary>Set when the escalation concerns one specific temporary issue rather than the outage as a whole.</summary>
    public Guid? TemporaryControlledIssueId { get; set; }

    public DowntimeEscalationType EscalationType { get; set; } = DowntimeEscalationType.DowntimeExceedsTwoWorkingDays;
    public DowntimeEscalationSeverity Severity { get; set; } = DowntimeEscalationSeverity.Major;
    public DowntimeEscalationRole RequiredRole { get; set; } = DowntimeEscalationRole.GQD;
    public DowntimeEscalationStatus Status { get; set; } = DowntimeEscalationStatus.Open;

    public required string Description { get; set; }
    public string? EvidenceReference { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
