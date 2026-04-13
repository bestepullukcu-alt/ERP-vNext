namespace Diten.Application.Dtos.EnterpriseStrategy;

/// <summary>
/// Typed Goal fields for create-from-template UI prefill (Strategy Library Goal template detail).
/// Version-aware: use with <see cref="StrategyTemplateDetailDto.Version"/> or template versions API.
/// </summary>
public sealed class GoalTemplatePrefillDto
{
    public string TemplateId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public int? PlanningStartYear { get; set; }
    public int? PlanningEndYear { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
    public string? ChangeLogRef { get; set; }
    public string? Tags { get; set; }
}
