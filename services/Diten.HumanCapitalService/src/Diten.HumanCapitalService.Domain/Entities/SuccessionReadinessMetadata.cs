using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class SuccessionReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SuccessionReadinessState SuccessionReadinessState { get; set; }
    public SuccessionReadinessState SuccessionPoolBoundaryState { get; set; }
    public SuccessionReadinessState HighPotentialIdentificationBoundaryState { get; set; }
    public SuccessionReadinessState NominationWorkflowBoundaryState { get; set; }
    public SuccessionReadinessState ReadinessAssessmentBoundaryState { get; set; }
    public SuccessionReadinessState TalentReviewBoundaryState { get; set; }
    public SuccessionReadinessState AutomatedDecisionBoundaryState { get; set; }
    public SuccessionReadinessState TalentProfileDependencyState { get; set; }
    public SuccessionReadinessState PositionFrameworkDependencyState { get; set; }
    public SuccessionReadinessState DocumentDependencyState { get; set; }
    public SuccessionReadinessState NotificationDependencyState { get; set; }
    public SuccessionReadinessState ConsentPreconditionState { get; set; }
    public SuccessionReadinessState DataMinimizationState { get; set; }
    public SuccessionReadinessState RetentionPolicyState { get; set; }
    public SuccessionReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, SuccessionReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long SuccessionReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
