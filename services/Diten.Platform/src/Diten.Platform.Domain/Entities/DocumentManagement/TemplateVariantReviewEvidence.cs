using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU18 — one recorded governance act against a variant: a bilingual review, a local approval, a
/// translation verification, a local adoption decision, a parent change assessment or a temporary English master
/// allowance (GMG-QMS-SOP-0001 §13.2).
///
/// APPEND-ONLY. A rejection is recorded as its own evidence row and is never erased or overwritten by a later
/// approval — the full sequence of decisions stays visible, which is what makes the translation trail auditable.
/// The evidence is a REFERENCE (a record locator), never the reviewed content itself.
/// </summary>
public sealed class TemplateVariantReviewEvidence : TenantScopedEntity
{
    public required Guid TemplateVariantId { get; set; }
    public VariantReviewEvidenceType EvidenceType { get; set; } = VariantReviewEvidenceType.BilingualReview;
    public VariantReviewEvidenceStatus Status { get; set; } = VariantReviewEvidenceStatus.Pending;

    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByRole { get; set; }
    public DateTimeOffset? PerformedAt { get; set; }

    /// <summary>Mandatory for a Completed record. A reference — never the reviewed document bytes.</summary>
    public required string EvidenceReference { get; set; }

    public string? Comment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
