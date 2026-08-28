namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU09 — approval route + evidence API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved.

public sealed class ResolveApprovalRouteApiRequest
{
    public bool? HasRaImpact { get; set; }
    public bool? HasPvImpact { get; set; }
    public bool? HasBatchReleaseImpact { get; set; }
    public bool? HasDmsCsvImpact { get; set; }
    public bool? HasQualityAgreementImpact { get; set; }
    public bool? IsGroupGovernance { get; set; }
    public bool? RequiresLegalReview { get; set; }
    public bool? RequiresCeoEndorsement { get; set; }
    public bool? RequiresIndependentTechnicalReview { get; set; }
    public Guid? AuthorUserId { get; set; }
    public Guid? RequestedByUserId { get; set; }
}

public sealed class RecordApprovalEvidenceApiRequest
{
    public Guid RequirementId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid PerformedByUserId { get; set; }
    public string PerformedByRole { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
    public string? Comment { get; set; }
}

public sealed class RejectApprovalApiRequest
{
    public Guid RequirementId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string PerformedByRole { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
