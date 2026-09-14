using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class IndustryTalentPoolReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IndustryTalentPoolReadinessState IndustryTalentPoolReadinessState { get; set; }
    public IndustryTalentPoolReadinessState PoolMembershipCatalogBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState CandidateInclusionIntakeBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState EligibilityScopeBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState VisibilityControlBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState PoolCurationReviewBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState AutomatedDecisionBoundaryState { get; set; }
    public IndustryTalentPoolReadinessState TalentDataSourceDependencyState { get; set; }
    public IndustryTalentPoolReadinessState ConsentPolicyDependencyState { get; set; }
    public IndustryTalentPoolReadinessState ReputationSourceDependencyState { get; set; }
    public IndustryTalentPoolReadinessState NotificationDependencyState { get; set; }
    public IndustryTalentPoolReadinessState ConsentPreconditionState { get; set; }
    public IndustryTalentPoolReadinessState DataMinimizationState { get; set; }
    public IndustryTalentPoolReadinessState RetentionPolicyState { get; set; }
    public IndustryTalentPoolReadinessState EligibilityPolicyState { get; set; }
    public Dictionary<string, IndustryTalentPoolReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long IndustryTalentPoolReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
