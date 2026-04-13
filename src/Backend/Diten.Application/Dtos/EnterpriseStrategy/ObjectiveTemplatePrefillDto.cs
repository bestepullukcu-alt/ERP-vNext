namespace Diten.Application.Dtos.EnterpriseStrategy;

/// <summary>
/// Typed Objective fields for create-from-template UI prefill and advisory hints.
/// Parent Goal inheritance remains authoritative; horizon and entity scope are advisory only.
/// </summary>
public sealed class ObjectiveTemplatePrefillDto
{
    public string TemplateId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public DateTime? TimeHorizonStart { get; set; }
    public DateTime? TimeHorizonEnd { get; set; }
    public string? DependencyNotes { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
}
