namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU13 — suspension / retirement / temporary-instruction API request payloads (JSON from the TenantShell
// proxy). TenantId is never accepted from the client; it is server-side resolved.

public sealed class OpenSuspensionCaseApiRequest
{
    public string TriggerType { get; set; } = string.Empty;
    public string TriggerDescription { get; set; } = string.Empty;
    public Guid? SourcePeriodicReviewEscalationId { get; set; }
}

public sealed class EscalateSuspensionCaseApiRequest
{
    public string? Comment { get; set; }
}

public sealed class ApproveSuspensionApiRequest
{
    public string Decision { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public string ApprovedByRole { get; set; } = string.Empty;
    public string CommunicationPlanReference { get; set; } = string.Empty;
}

public sealed class RejectSuspensionApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ExecuteSuspensionApiRequest
{
    public string SuspensionNoticeReference { get; set; } = string.Empty;
    public string AccessRemovalEvidenceReference { get; set; } = string.Empty;
    public string AffectedRecordsBatchesActivitiesReference { get; set; } = string.Empty;
}

public sealed class CloseSuspensionCaseApiRequest
{
    public string? DeviationReference { get; set; }
    public string? CorrectiveActionReference { get; set; }
    public string? ReplacementPlanReference { get; set; }
}

public sealed class RequestRetirementApiRequest
{
    public string RetirementReason { get; set; } = string.Empty;
    public string JustificationReference { get; set; } = string.Empty;
    public string TransitionAssessmentReference { get; set; } = string.Empty;
    public string? ReplacementDocumentUid { get; set; }
    public string? ReplacementDocumentCode { get; set; }
}

public sealed class ApproveRetirementApiRequest
{
    public string ApprovedByRole { get; set; } = string.Empty;
}

public sealed class RejectRetirementApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ExecuteRetirementApiRequest
{
    public string CommunicationEvidenceReference { get; set; } = string.Empty;
    public string ArchivalEvidenceReference { get; set; } = string.Empty;
}

public sealed class StartTemporaryInstructionApiRequest
{
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
}

public sealed class CloseTemporaryInstructionApiRequest
{
    public string ExpiryAction { get; set; } = string.Empty;
    public string? ExpiryActionEvidenceReference { get; set; }
    public Guid? ReplacementRegisterEntryId { get; set; }
}
