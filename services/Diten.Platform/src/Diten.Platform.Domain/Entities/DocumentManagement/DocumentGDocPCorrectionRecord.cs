using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU21 — the GDocP / ALCOA+ correction trail entry for ONE regulated field of ONE regulated record
/// (GMG-QMS-SOP-0001 §21).
///
/// WHAT THE SOP REQUIRES AND THIS CARRIES: the previous value stays legible next to the new value, the reason is
/// mandatory, and the corrector plus the moment of correction are stamped BY THE SERVER. That combination is what
/// makes a correction attributable, contemporaneous and original rather than a silent overwrite.
///
/// APPEND-ONLY. A correction record is never edited into a different shape and never hard-deleted. Reviewing or
/// rejecting it sets the review fields; it never rewrites the recorded values, because the correction trail is the
/// evidence that the correction happened at all.
///
/// NO CONTENT: snapshots are textual representations of FIELD VALUES, never document bytes. A value withheld for
/// confidentiality is marked <see cref="GDocPValueFormat.Redacted"/> with an explicit marker — never blanked.
/// </summary>
public sealed class DocumentGDocPCorrectionRecord : TenantScopedEntity
{
    /// <summary>Explicit sentinel for a previous value that could not be established. Never an empty string.</summary>
    public const string UnknownPreviousValue = "UNKNOWN_OR_UNAVAILABLE";

    /// <summary>Explicit marker for a deliberately withheld value.</summary>
    public const string RedactedMarker = "[REDACTED]";

    public required string CorrectionNumber { get; set; }

    // ── What was corrected ───────────────────────────────────────────────────────────────────────────────
    public GDocPSubjectType SubjectType { get; set; } = GDocPSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }

    /// <summary>The corrected field, e.g. "EffectiveDate" or "ApprovalEvidenceReference". Mandatory.</summary>
    public required string FieldPath { get; set; }
    public string? FieldDisplayName { get; set; }

    // ── The values (SOP §21 — the previous value must remain legible) ────────────────────────────────────
    public required string PreviousValueSnapshot { get; set; }
    public required string NewValueSnapshot { get; set; }
    public GDocPValueFormat ValueFormat { get; set; } = GDocPValueFormat.Text;

    // ── Why ──────────────────────────────────────────────────────────────────────────────────────────────
    public GDocPCorrectionType CorrectionType { get; set; } = GDocPCorrectionType.MetadataCorrection;
    public required string CorrectionReason { get; set; }
    public string? CorrectionEvidenceReference { get; set; }

    // ── Risk classification (computed by the evaluator, never client-asserted) ───────────────────────────
    public bool IsHighRiskCorrection { get; set; }
    public bool RequiresDeviationReference { get; set; }
    public string? DeviationReference { get; set; }

    /// <summary>True when a regulated timestamp was moved EARLIER — the backdating signal (SOP §21).</summary>
    public bool IsBackdatingCorrection { get; set; }

    /// <summary>Human-readable explanation of how the risk classification was reached.</summary>
    public string? RiskAssessmentNote { get; set; }

    // ── Who and when (SERVER-STAMPED — never accepted from the client) ───────────────────────────────────
    public Guid? CorrectedByUserId { get; set; }
    public string? CorrectedByRole { get; set; }

    /// <summary>Always DateTimeOffset.UtcNow at record time. A client-supplied value is never honoured.</summary>
    public DateTimeOffset CorrectedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? RequestedBy { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }

    // ── Second-person review ─────────────────────────────────────────────────────────────────────────────
    public GDocPReviewStatus ReviewStatus { get; set; } = GDocPReviewStatus.NotRequired;
    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewEvidenceReference { get; set; }
    public string? ReviewComment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>A decided review is final — it can never be re-reviewed into a different verdict.</summary>
    public bool IsReviewDecided() => ReviewStatus is GDocPReviewStatus.Reviewed or GDocPReviewStatus.Rejected;
}
