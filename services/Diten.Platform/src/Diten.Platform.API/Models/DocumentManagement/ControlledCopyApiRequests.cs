namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU17 — controlled copy API request payloads (JSON from the TenantShell proxy). TenantId is never accepted
// from the client; it is server-side resolved.

public sealed class RegisterControlledCopyApiRequest
{
    public string CopyType { get; set; } = string.Empty;
    public int? CopyNumber { get; set; }
    public string? LocationType { get; set; }
    public string? LocationDescription { get; set; }
    public Guid? HolderUserId { get; set; }
    public string? HolderRole { get; set; }
    public string? HolderDepartment { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? ControlledDocumentVersionId { get; set; }
    public Guid? RepositoryAssessmentId { get; set; }
}

public sealed class WithdrawControlledCopyApiRequest
{
    public string WithdrawalEvidenceReference { get; set; } = string.Empty;
}

public sealed class ReconcileControlledCopyApiRequest
{
    public string ReconciliationEvidenceReference { get; set; } = string.Empty;
}

public sealed class MarkControlledCopyMissingApiRequest
{
    public string? Comment { get; set; }
}

public sealed class MarkControlledCopyObsoleteApiRequest
{
    public string ObsoleteReason { get; set; } = string.Empty;
    public string? LocationDescription { get; set; }
}

public sealed class GenerateWithdrawalPlanApiRequest
{
    public string? TriggerType { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}

public sealed class CompleteWithdrawalPlanApiRequest
{
    public string? PlanEvidenceReference { get; set; }
    public string? MissingDeviationReference { get; set; }
}

public sealed class ResolveObsoleteCopyFindingApiRequest
{
    public string ResolutionEvidenceReference { get; set; } = string.Empty;
    public string? DeviationReference { get; set; }
    public string? QualityEventReference { get; set; }
}
