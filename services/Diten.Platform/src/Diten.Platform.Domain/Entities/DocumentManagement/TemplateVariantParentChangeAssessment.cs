using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU18 — the record of what was observed about a variant's parent at a point in time, and what the
/// variant must therefore do (GMG-QMS-SOP-0001 §13.2).
///
/// SOP rules this captures:
/// • A master becoming newly effective puts its variants into revision — the translation is Outdated until
///   reassessed and re-reviewed.
/// • A superseded / retired / suspended parent must stop the variant being used locally.
///
/// This is a GOVERNANCE assessment: it compares metadata and lineage, never document content. It produces
/// requirements and recommendations; it does not itself transition the variant's FU03 status or overwrite content.
/// Assessments accumulate as history and are never hard-deleted, so the parent-linkage trail is preserved.
/// </summary>
public sealed class TemplateVariantParentChangeAssessment : TenantScopedEntity
{
    public required Guid TemplateVariantId { get; set; }

    // ── What the parent looked like at assessment time ───────────────────────────────────────────────────
    public Guid? ParentTemplateMasterId { get; set; }
    public Guid? ParentTemplateMasterVersionId { get; set; }
    public string? ParentDocumentUid { get; set; }
    public string? ParentDocumentCode { get; set; }
    public ObservedParentStatus ObservedParentStatus { get; set; } = ObservedParentStatus.Unknown;
    public string? ObservedParentVersionLabel { get; set; }
    public int? ObservedParentVersionNumber { get; set; }
    public DateTimeOffset? ObservedParentEffectiveDate { get; set; }

    // ── What must happen next ────────────────────────────────────────────────────────────────────────────
    public ParentChangeAssessmentStatus AssessmentStatus { get; set; } = ParentChangeAssessmentStatus.InSync;
    public bool RequiresVariantRevision { get; set; }
    public bool RequiresBilingualReview { get; set; }
    public bool RequiresLocalApproval { get; set; }

    /// <summary>True when the parent is superseded/retired/suspended — local use must stop.</summary>
    public bool RequiresSuspension { get; set; }

    public string? AssessmentEvidenceReference { get; set; }
    public string? AssessmentNote { get; set; }

    public DateTimeOffset AssessedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AssessedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
