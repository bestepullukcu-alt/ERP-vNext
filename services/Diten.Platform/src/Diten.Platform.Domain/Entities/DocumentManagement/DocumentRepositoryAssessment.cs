using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU16 — a repository / DMS assessment (GMG-QMS-SOP-0001 §11.1). An interim repository shall not be used until
/// an approved repository assessment exists that defines, at minimum: named owner, exact location, access model,
/// access-review frequency, backup, restore test, approval mechanism, effective-copy control, a maximum interim period
/// with a checkpoint, and migration reconciliation. This is a GOVERNANCE + EVIDENCE record — it does NOT validate a
/// system, run backups, or implement e-signature; it records what has been assessed and which category the repository
/// falls into. An approved assessment can be superseded but never hard-deleted.
/// </summary>
public sealed class DocumentRepositoryAssessment : TenantScopedEntity
{
    public required string RepositoryKey { get; set; }
    public required string RepositoryName { get; set; }
    public RepositoryType RepositoryType { get; set; }
    public RepositoryAssessmentStatus AssessmentStatus { get; set; } = RepositoryAssessmentStatus.Draft;

    public Guid? RepositoryOwnerUserId { get; set; }
    public string? RepositoryOwnerRole { get; set; }

    public string? ExactLocation { get; set; }
    public RepositoryLocationType LocationType { get; set; } = RepositoryLocationType.InHouseSoftware;

    public string? AccessModelDescription { get; set; }
    public string? AccessReviewFrequency { get; set; }
    public DateTimeOffset? LastAccessReviewDate { get; set; }
    public DateTimeOffset? NextAccessReviewDueDate { get; set; }

    public string? BackupMethodDescription { get; set; }
    public DateTimeOffset? LastBackupVerificationDate { get; set; }
    public string? RestoreTestFrequency { get; set; }
    public DateTimeOffset? LastRestoreTestDate { get; set; }

    public string? ApprovalMechanismDescription { get; set; }
    public string? EffectiveCopyControlDescription { get; set; }
    public string? AuditTrailDescription { get; set; }
    public string? ChangeControlDescription { get; set; }

    /// <summary>Validation evidence for a DMS used for regulated electronic approval (SOP §11.2). A reference only —
    /// FU16 does not run CSV/validation.</summary>
    public string? ValidationEvidenceReference { get; set; }

    // Interim controls (SOP §11.1).
    public int? MaxInterimPeriodDays { get; set; }
    public DateTimeOffset? InterimCheckpointDueDate { get; set; }
    public bool MigrationReconciliationRequired { get; set; }
    public string? MigrationReconciliationReference { get; set; }

    public string? AssessmentEvidenceReference { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByRole { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
