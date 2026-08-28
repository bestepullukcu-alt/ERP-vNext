using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU17 — a controlled copy of a document at a point of use (GMG-QMS-SOP-0001 §18 LOG-0002 Controlled Copy
/// Log). Tracks digital effective copies and printed/external controlled copies, their holder/location, and their
/// withdrawal/reconciliation lifecycle. A copy is never hard-deleted — its history (issued → withdrawn → reconciled,
/// or detected obsolete) is permanent. Quality-event / deviation links are REFERENCES only (no CAPA module here).
/// </summary>
public sealed class DocumentControlledCopy : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? ControlledDocumentVersionId { get; set; }

    public int CopyNumber { get; set; }
    public ControlledCopyType CopyType { get; set; }
    public ControlledCopyStatus CopyStatus { get; set; } = ControlledCopyStatus.Active;

    public ControlledCopyLocationType LocationType { get; set; } = ControlledCopyLocationType.PointOfUse;
    public string? LocationDescription { get; set; }
    public Guid? HolderUserId { get; set; }
    public string? HolderRole { get; set; }
    public string? HolderDepartment { get; set; }
    public Guid? RepositoryAssessmentId { get; set; }

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? IssuedBy { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }

    public bool WithdrawalRequired { get; set; }
    public DateTimeOffset? WithdrawalDueDate { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public string? WithdrawnBy { get; set; }
    public string? WithdrawalEvidenceReference { get; set; }

    public DateTimeOffset? ReconciledAt { get; set; }
    public string? ReconciledBy { get; set; }
    public string? ReconciliationEvidenceReference { get; set; }

    public DateTimeOffset? ObsoleteDetectedAt { get; set; }
    public string? ObsoleteDetectedBy { get; set; }
    public string? ObsoleteReason { get; set; }

    public string? QualityEventReference { get; set; }
    public string? DeviationReference { get; set; }
    public string? Comment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
