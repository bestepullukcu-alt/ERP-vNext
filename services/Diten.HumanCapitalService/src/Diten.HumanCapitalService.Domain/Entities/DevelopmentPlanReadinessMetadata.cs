using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class DevelopmentPlanReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DevelopmentPlanReadinessState DevelopmentPlanReadinessState { get; set; }
    public DevelopmentPlanReadinessState DevelopmentPlanWorkflowBoundaryState { get; set; }
    public DevelopmentPlanReadinessState GoalAssignmentBoundaryState { get; set; }
    public DevelopmentPlanReadinessState LearningAssignmentBoundaryState { get; set; }
    public DevelopmentPlanReadinessState SkillGapScoringBoundaryState { get; set; }
    public DevelopmentPlanReadinessState RatingBoundaryState { get; set; }
    public DevelopmentPlanReadinessState RecommendationBoundaryState { get; set; }
    public DevelopmentPlanReadinessState RankingBoundaryState { get; set; }
    public DevelopmentPlanReadinessState AutomatedDecisionBoundaryState { get; set; }
    public DevelopmentPlanReadinessState ManagerActionUxBoundaryState { get; set; }
    public DevelopmentPlanReadinessState CoachingActionBoundaryState { get; set; }
    public DevelopmentPlanReadinessState DocumentDependencyState { get; set; }
    public DevelopmentPlanReadinessState NotificationDependencyState { get; set; }
    public DevelopmentPlanReadinessState ConsentPreconditionState { get; set; }
    public DevelopmentPlanReadinessState DataMinimizationState { get; set; }
    public DevelopmentPlanReadinessState RetentionPolicyState { get; set; }
    public DevelopmentPlanReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, DevelopmentPlanReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long DevelopmentPlanReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
