namespace Diten.Web.Models.DemandIdeas;

public sealed class DemandIdeaListItemDto
{
    public string Id { get; init; } = "";
    public string RecordNumber { get; init; } = "";
    public string Title { get; init; } = "";
    public string Classification { get; init; } = "";
    public string RequestType { get; init; } = "";
    public string Priority { get; init; } = "";
    public string Status { get; init; } = "";
    public string OwnerName { get; init; } = "";
    public string OwnerInitials { get; init; } = "";
    public string SponsorName { get; init; } = "";
    public string SponsorInitials { get; init; } = "";
    public string BusinessUnit { get; init; } = "";
    public DateTime? SubmittedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReviewDueDate { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string Category { get; init; } = "";
    public string DemandSource { get; init; } = "";
    public string StrategicTheme { get; init; } = "";
    public string ComplianceImpact { get; init; } = "";
    public string EstimatedComplexity { get; init; } = "";
    public string RiskSensitivity { get; init; } = "";
    public string? ReviewerName { get; init; }
    public string RequestorName { get; init; } = "";
    public IReadOnlyList<string> Stakeholders { get; init; } = Array.Empty<string>();
    public string LinkedInitiativeId { get; init; } = "";
    public string LinkedProjectId { get; init; } = "";
    public string TransferTargetType { get; init; } = "";
    public string PortfolioLinkStatus { get; init; } = "";
    public string Criticality { get; init; } = "";
    public bool HasAttachments { get; init; }
    public bool HasSupportingLinks { get; init; }
    public bool HasComments { get; init; }
    public bool HasDuplicatesFlagged { get; init; }
    public bool HasRelatedIdeas { get; init; }
    public string CreatedBy { get; init; } = "";
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public IReadOnlyList<string> RelatedIdeaIds { get; init; } = Array.Empty<string>();
    public bool IsTransferred { get; init; }
    public string? TransferTargetId { get; init; }
    public DateTime? TransferDate { get; init; }
    public string? TransferStatus { get; init; }
    public string? TransferredBy { get; init; }
    public bool CanTransfer { get; init; }
}
