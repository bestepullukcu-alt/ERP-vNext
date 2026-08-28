namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU11 — training matrix API request payloads (JSON from the TenantShell proxy). TenantId is never accepted
// from the client; it is server-side resolved.

public sealed class AddManualTrainingRequirementApiRequest
{
    public string AudienceType { get; set; } = string.Empty;
    public string? RequiredRole { get; set; }
    public Guid? RequiredUserId { get; set; }
    public string? RequiredDepartment { get; set; }
    public string TrainingType { get; set; } = string.Empty;
    public bool IsCriticalProcessUserRequirement { get; set; }
    public bool EffectivenessCheckRequired { get; set; }
    public bool AcknowledgementRequired { get; set; }
    public bool MandatoryBeforeEffective { get; set; } = true;
}

public sealed class AssignTrainingApiRequest
{
    public Guid RequirementId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToRole { get; set; }
    public string? AssignedToDepartment { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}

public sealed class CompleteTrainingApiRequest
{
    public string CompletionEvidenceReference { get; set; } = string.Empty;
}

public sealed class RecordTrainingEffectivenessApiRequest
{
    public bool Passed { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class RestrictTrainingApiRequest
{
    public string Reason { get; set; } = string.Empty;
}
