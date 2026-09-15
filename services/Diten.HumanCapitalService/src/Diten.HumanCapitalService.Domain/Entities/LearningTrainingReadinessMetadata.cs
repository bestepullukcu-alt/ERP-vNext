using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class LearningTrainingReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public LearningTrainingReadinessState LearningTrainingReadinessState { get; set; }
    public LearningTrainingReadinessState CourseCatalogBoundaryState { get; set; }
    public LearningTrainingReadinessState EnrollmentWorkflowBoundaryState { get; set; }
    public LearningTrainingReadinessState CompletionTrackingBoundaryState { get; set; }
    public LearningTrainingReadinessState CertificationBoundaryState { get; set; }
    public LearningTrainingReadinessState AssessmentScoringBoundaryState { get; set; }
    public LearningTrainingReadinessState AutomatedDecisionBoundaryState { get; set; }
    public LearningTrainingReadinessState LearningContentDependencyState { get; set; }
    public LearningTrainingReadinessState SkillTaxonomyDependencyState { get; set; }
    public LearningTrainingReadinessState DocumentDependencyState { get; set; }
    public LearningTrainingReadinessState NotificationDependencyState { get; set; }
    public LearningTrainingReadinessState ConsentPreconditionState { get; set; }
    public LearningTrainingReadinessState DataMinimizationState { get; set; }
    public LearningTrainingReadinessState RetentionPolicyState { get; set; }
    public LearningTrainingReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, LearningTrainingReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long LearningTrainingReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
