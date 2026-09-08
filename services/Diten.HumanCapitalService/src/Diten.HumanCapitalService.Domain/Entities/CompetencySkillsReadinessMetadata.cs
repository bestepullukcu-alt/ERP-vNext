using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class CompetencySkillsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CompetencySkillsReadinessState CompetencySkillsReadinessState { get; set; }
    public CompetencySkillsReadinessState AssessmentWorkflowBoundaryState { get; set; }
    public CompetencySkillsReadinessState CompetencyFrameworkDependencyState { get; set; }
    public CompetencySkillsReadinessState SkillTaxonomyDependencyState { get; set; }
    public CompetencySkillsReadinessState SkillScoringBoundaryState { get; set; }
    public CompetencySkillsReadinessState RatingBoundaryState { get; set; }
    public CompetencySkillsReadinessState CalibrationBoundaryState { get; set; }
    public CompetencySkillsReadinessState RankingBoundaryState { get; set; }
    public CompetencySkillsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public CompetencySkillsReadinessState ManagerAssessmentUxBoundaryState { get; set; }
    public CompetencySkillsReadinessState EmployeeAssessmentUxBoundaryState { get; set; }
    public CompetencySkillsReadinessState DocumentDependencyState { get; set; }
    public CompetencySkillsReadinessState NotificationDependencyState { get; set; }
    public CompetencySkillsReadinessState ConsentPreconditionState { get; set; }
    public CompetencySkillsReadinessState DataMinimizationState { get; set; }
    public CompetencySkillsReadinessState RetentionPolicyState { get; set; }
    public CompetencySkillsReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, CompetencySkillsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long CompetencySkillsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
