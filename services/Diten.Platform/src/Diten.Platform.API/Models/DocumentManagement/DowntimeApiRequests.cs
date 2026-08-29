namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU20 — downtime / temporary controlled issue API request payloads (JSON from the TenantShell proxy).
// TenantId is never accepted from the client; it is server-side resolved. Every evidence field is a REFERENCE
// string — no incident report, signed approval or reconciliation document content is ever transmitted or stored.

public sealed class OpenDowntimeEventApiRequest
{
    public string DetectionEvidenceReference { get; set; } = string.Empty;
    public string? DowntimeType { get; set; }
    public Guid? RepositoryAssessmentId { get; set; }
    public string? RepositoryName { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public Guid? DetectedByUserId { get; set; }
    public string? ImpactSummary { get; set; }
}

public sealed class MarkRepositoryRestoredApiRequest
{
    public string RestoreEvidenceReference { get; set; } = string.Empty;
    public DateTimeOffset? RestoredAt { get; set; }
}

public sealed class CloseDowntimeEventApiRequest
{
    /// <summary>Mandatory when the outage exceeded 2 working days (SOP §11.3). A reference only.</summary>
    public string? BcpAssessmentReference { get; set; }
    public string? ClosureNote { get; set; }
}

public sealed class RequestTemporaryIssueApiRequest
{
    public Guid RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? ControlledDocumentVersionId { get; set; }
    public string? IssueReason { get; set; }
    public string? RecipientRole { get; set; }
    public string? RecipientDepartment { get; set; }
    public List<Guid>? RecipientUserIds { get; set; }
}

public sealed class ApproveTemporaryIssueApiRequest
{
    public string ApprovedByRole { get; set; } = string.Empty;

    /// <summary>What the approver states was used. FU20 validates no signature and claims no e-signature.</summary>
    public string ApprovalMechanism { get; set; } = string.Empty;

    public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
}

public sealed class IssueTemporaryControlledCopyApiRequest
{
    public int IssuedCopyCount { get; set; }
    public string TemporaryLocationDescription { get; set; } = string.Empty;
    public string? LocationType { get; set; }
}

public sealed class ReconcileTemporaryIssueApiRequest
{
    public string ReconciliationEvidenceReference { get; set; } = string.Empty;

    /// <summary>Mandatory when reconciling after the 3-working-day window has passed.</summary>
    public string? DeviationReference { get; set; }

    public string? CorrectiveActionReference { get; set; }
    public string? MissingReconciliationReason { get; set; }

    /// <summary>True when the physical copies were pulled back rather than reconciled in place.</summary>
    public bool WithdrawCopiesInsteadOfReconcile { get; set; }
}

public sealed class CancelTemporaryIssueApiRequest
{
    public string Reason { get; set; } = string.Empty;
}
