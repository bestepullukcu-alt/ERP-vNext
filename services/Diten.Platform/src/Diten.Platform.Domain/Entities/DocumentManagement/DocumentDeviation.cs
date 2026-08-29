using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU22 — a GxP deviation arising from a document-control quality event (GMG-QMS-SOP-0001).
///
/// ⚠️ NOT <see cref="DocumentCollectionDeviation"/>. That is MOD-0028-FU09: a read-back mismatch between the
/// expected and the actually-provisioned folder tree — an infrastructure qualification finding. THIS is a
/// regulated quality deviation with root cause, patient/product/regulatory impact and CAPA. The two are unrelated
/// and must never be merged; their enums are prefixed differently for the same reason.
///
/// A deviation always hangs off a quality event: a deviation with no originating event has no context to be
/// assessed against.
///
/// CLOSURE IS GATED (SOP): a critical deviation cannot close without a root cause AND an impact assessment, and a
/// deviation that required CAPA cannot close while its actions are outstanding. FU22 implements no investigation
/// module and no root-cause methodology — it records what a human concluded. Never hard-deleted.
/// </summary>
public sealed class DocumentDeviation : TenantScopedEntity
{
    public required string DeviationNumber { get; set; }

    /// <summary>Mandatory linkage — a deviation is always the consequence of a recorded quality event.</summary>
    public required Guid QualityEventId { get; set; }

    public required string DeviationTitle { get; set; }
    public required string DeviationDescription { get; set; }

    public QualityDeviationCategory DeviationCategory { get; set; } = QualityDeviationCategory.DocumentationControl;
    public QualityDeviationSeverity DeviationSeverity { get; set; } = QualityDeviationSeverity.Minor;
    public QualityDeviationStatus DeviationStatus { get; set; } = QualityDeviationStatus.Draft;

    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReportedBy { get; set; }

    // ── Investigation outcome (recorded, never computed) ─────────────────────────────────────────────────
    public string? RootCauseSummary { get; set; }
    public DeviationRootCauseCategory RootCauseCategory { get; set; } = DeviationRootCauseCategory.NotAssessed;

    public string? ImpactAssessmentSummary { get; set; }
    public DeviationImpactAssessment PatientProductRegulatoryImpact { get; set; } = DeviationImpactAssessment.NotAssessed;

    public string? InvestigationEvidenceReference { get; set; }

    // ── CAPA ─────────────────────────────────────────────────────────────────────────────────────────────
    public bool RequiresCAPA { get; set; }
    public List<Guid> CAPAActionIds { get; set; } = [];

    // ── Closure ──────────────────────────────────────────────────────────────────────────────────────────
    public string? ClosureEvidenceReference { get; set; }

    /// <summary>Documented basis for closing despite an outstanding requirement. Auditable, unlike a silent skip.</summary>
    public string? ClosureExceptionJustification { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? CancellationReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsSettled() => DeviationStatus is QualityDeviationStatus.Closed or QualityDeviationStatus.Cancelled;
}
