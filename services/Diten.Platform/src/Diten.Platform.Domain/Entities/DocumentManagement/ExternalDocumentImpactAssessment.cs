using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU14 — the impact assessment raised when an external document is registered or changes
/// (GMG-QMS-SOP-0001 §10.3). Where GMP/GDP/PV/RA impact exists the assessment shall be completed within
/// 10 working days of the trigger; <see cref="DueDate"/> carries that computed deadline.
///
/// BOUNDARY: <see cref="RecommendedAction"/> is a RECOMMENDATION. Completing an assessment never transitions,
/// suspends or retires an internal controlled document — the FU08 lifecycle engine and FU13 suspension engine
/// remain the only paths for that. Likewise <see cref="ExternalImpactRecommendedAction.QualityEventReview"/> is a
/// referral, not a CAPA/Quality Event record: FU14 implements no CAPA module.
/// Assessments are never hard-deleted.
/// </summary>
public sealed class ExternalDocumentImpactAssessment : TenantScopedEntity
{
    public required Guid ExternalDocumentRegisterEntryId { get; set; }

    public ExternalImpactAssessmentStatus AssessmentStatus { get; set; } = ExternalImpactAssessmentStatus.Pending;
    public ExternalImpactTriggerType TriggerType { get; set; } = ExternalImpactTriggerType.Manual;

    /// <summary>Trigger date + 10 working days where a GMP/GDP/PV/RA impact is flagged (SOP §10.3).</summary>
    public DateTimeOffset DueDate { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public Guid? CompletedByUserId { get; set; }

    /// <summary>Mandatory to complete the assessment (SOP §10.3). A reference — never the document bytes.</summary>
    public string? AssessmentEvidenceReference { get; set; }

    public string? ImpactSummary { get; set; }

    public bool HasGmpImpact { get; set; }
    public bool HasGdpImpact { get; set; }
    public bool HasPvImpact { get; set; }
    public bool HasRaImpact { get; set; }
    public bool HasBatchReleaseImpact { get; set; }
    public bool HasTrainingImpact { get; set; }
    public bool HasDocumentImpact { get; set; }

    public ExternalImpactRecommendedAction RecommendedAction { get; set; } = ExternalImpactRecommendedAction.NoAction;

    // ── Follow-up ownership. FU14 records the commitment; it does not run the action. ────────────────────
    public Guid? ActionOwnerUserId { get; set; }
    public string? ActionOwnerRole { get; set; }
    public DateTimeOffset? ActionDueDate { get; set; }

    /// <summary>Pointer to the change control / CAPA / training record that carries the action forward.</summary>
    public string? ActionReference { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
