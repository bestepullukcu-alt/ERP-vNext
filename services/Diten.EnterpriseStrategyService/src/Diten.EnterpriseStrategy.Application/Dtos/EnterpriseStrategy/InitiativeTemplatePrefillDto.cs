namespace Diten.Application.Dtos.EnterpriseStrategy;

/// <summary>
/// Typed Initiative fields for create-from-template UI prefill and advisory hints.
/// Parent Objective and Parent Goal runtime inheritance remain authoritative.
/// </summary>
public sealed class InitiativeTemplatePrefillDto
{
    public string TemplateId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string ParentObjectiveTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
    public string AccountableSponsorRole { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string BudgetEnvelope { get; set; } = string.Empty;
    public string MaturityReadiness { get; set; } = string.Empty;
    public string InitiativeClass { get; set; } = string.Empty;
    public string ContributionMethod { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string StrategyAlignmentNote { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
}
