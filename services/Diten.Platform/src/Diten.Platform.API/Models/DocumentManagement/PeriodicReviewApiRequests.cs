namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU12 — periodic review API request payloads (JSON from the TenantShell proxy). TenantId is never accepted
// from the client; it is server-side resolved.

public sealed class CompletePeriodicReviewApiRequest
{
    public string Decision { get; set; } = string.Empty;
    public string ReviewEvidenceReference { get; set; } = string.Empty;
    public string? ImpactAssessmentReference { get; set; }
    public string? Comment { get; set; }
}

public sealed class RequestPeriodicReviewExtensionApiRequest
{
    public int ExtensionDays { get; set; }
    public string RiskAssessmentReference { get; set; } = string.Empty;
    public string? Justification { get; set; }
}

public sealed class ApprovePeriodicReviewExtensionApiRequest
{
    public string ApproverRole { get; set; } = string.Empty;
    public bool ManagementReviewEscalated { get; set; }
    public string? Comment { get; set; }
}

public sealed class RejectPeriodicReviewExtensionApiRequest
{
    public string Reason { get; set; } = string.Empty;
}
