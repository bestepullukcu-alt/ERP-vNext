namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class CreateStrategyLinkedContextDto
{
    public string ParentInitiativeId { get; set; } = string.Empty;
    public string? ParentObjectiveId { get; set; }
    public string? ParentGoalId { get; set; }
    public string? ScopeModeCode { get; set; }
    public string? PrimaryStrategyCompanyId { get; set; }
    public List<string> ApplicableCompanyIds { get; set; } = new();
    public string? StrategyTraceabilityNote { get; set; }
    public decimal ContributionWeight { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public bool CreateProjectTemplate { get; set; }
}
