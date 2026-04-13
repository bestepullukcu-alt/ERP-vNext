using Diten.Domain.Common;

namespace Diten.Domain.Aggregates.DemandIdea;

/// <summary>
/// Demand / idea capture record (early refinement). Status: Draft | Submitted (extensible).
/// </summary>
public sealed class DemandIdeaAggregate : BaseEntity
{
    /// <summary>Display id e.g. IC-2026-0042</summary>
    public string RecordNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string ProblemStatement { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;

    public string RequestType { get; set; } = string.Empty;
    public string StrategicAlignment { get; set; } = string.Empty;
    public string BusinessUnit { get; set; } = string.Empty;
    public string Requestor { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    /// <summary>Optional display owner; may mirror requestor.</summary>
    public string? OwnerName { get; set; }

    public string ProposedScope { get; set; } = string.Empty;
    public string OutOfScope { get; set; } = string.Empty;
    public string Assumptions { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
    public string DemandSource { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ComplianceImpact { get; set; } = string.Empty;
    public string EstimatedComplexity { get; set; } = string.Empty;
    public string RiskSensitivity { get; set; } = string.Empty;

    public List<string> SupportingLinks { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<DemandIdeaAttachment> Attachments { get; set; } = new();

    /// <summary>Linked strategic theme keys or labels (multi).</summary>
    public List<string> StrategicThemeKeys { get; set; } = new();

    /// <summary>Explicit links to other demand idea record IDs (same portfolio).</summary>
    public List<string> RelatedIdeaIds { get; set; } = new();

    public string Status { get; set; } = "Draft";
    public DateTime? ReviewDueDate { get; set; }
}
