namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU16 — repository assessment API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved.

public sealed class RepositoryAssessmentApiRequest
{
    public string RepositoryName { get; set; } = string.Empty;
    public string RepositoryType { get; set; } = string.Empty;
    public string? LocationType { get; set; }
    public Guid? RepositoryOwnerUserId { get; set; }
    public string? RepositoryOwnerRole { get; set; }
    public string? ExactLocation { get; set; }
    public string? AccessModelDescription { get; set; }
    public string? AccessReviewFrequency { get; set; }
    public string? BackupMethodDescription { get; set; }
    public string? RestoreTestFrequency { get; set; }
    public string? ApprovalMechanismDescription { get; set; }
    public string? EffectiveCopyControlDescription { get; set; }
    public string? AuditTrailDescription { get; set; }
    public string? ChangeControlDescription { get; set; }
    public string? ValidationEvidenceReference { get; set; }
    public int? MaxInterimPeriodDays { get; set; }
    public DateTimeOffset? InterimCheckpointDueDate { get; set; }
    public bool MigrationReconciliationRequired { get; set; }
    public string? MigrationReconciliationReference { get; set; }
    public string? AssessmentEvidenceReference { get; set; }
}

public sealed class ApproveRepositoryAssessmentApiRequest
{
    public string ApprovedByRole { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntil { get; set; }
}

public sealed class RejectRepositoryAssessmentApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class LinkRepositoryAssessmentApiRequest
{
    public Guid RepositoryAssessmentId { get; set; }
}
