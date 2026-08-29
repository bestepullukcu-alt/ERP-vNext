using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU20 — the log of a repository / DMS outage (GMG-QMS-SOP-0001 §11.3). The SOP requires this log to be
/// OPENED BEFORE any controlled issue is made outside the normal environment: without a downtime record there is
/// no legitimate basis for a temporary controlled issue at all, which is why every issue in FU20 hangs off one of
/// these events.
///
/// SOP controls this carries: detection evidence, restore evidence, the computed working-day duration, and — once
/// the outage exceeds 2 working days — the GQD + IT/CSV escalation and the BCP assessment reference required
/// before the event can be closed.
///
/// BOUNDARIES: FU20 implements no BCP module (the assessment is a reference), no CAPA module, and no e-signature.
/// It records governance. The event is never hard-deleted; cancellation and closure are status changes.
/// </summary>
public sealed class DocumentRepositoryDowntimeEvent : TenantScopedEntity
{
    public required string DowntimeNumber { get; set; }

    /// <summary>FU16 linkage. Optional: an outage can be logged for a repository with no assessment on file.</summary>
    public Guid? RepositoryAssessmentId { get; set; }
    public string? RepositoryName { get; set; }

    public DowntimeStatus DowntimeStatus { get; set; } = DowntimeStatus.Open;
    public DowntimeType DowntimeType { get; set; } = DowntimeType.UnplannedOutage;

    // ── Detection (SOP §11.3 — evidence is mandatory to open) ────────────────────────────────────────────
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? StartedBy { get; set; }
    public Guid? DetectedByUserId { get; set; }

    /// <summary>Mandatory. A reference to the incident/ticket record — never the incident document bytes.</summary>
    public required string DetectionEvidenceReference { get; set; }

    public string? ImpactSummary { get; set; }

    // ── Restoration ──────────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? RestoredAt { get; set; }
    public string? RestoredBy { get; set; }
    public string? RestoreEvidenceReference { get; set; }

    /// <summary>Computed Mon–Fri duration. Drives the 2-working-day escalation threshold.</summary>
    public int? DurationWorkingDays { get; set; }

    // ── Escalation (SOP §11.3: > 2 working days ⇒ GQD + IT/CSV + BCP assessment) ─────────────────────────
    public bool RequiresGqdItCsvEscalation { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public string? EscalationEvidenceReference { get; set; }

    /// <summary>Mandatory before close once the 2-working-day threshold has been exceeded. A reference only.</summary>
    public string? BcpAssessmentReference { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? ClosureNote { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>A temporary controlled issue may only be raised while the outage is live or being reconciled.</summary>
    public bool AcceptsTemporaryIssues() =>
        DowntimeStatus is DowntimeStatus.Open or DowntimeStatus.Restored
            or DowntimeStatus.ReconciliationInProgress or DowntimeStatus.Escalated;
}
