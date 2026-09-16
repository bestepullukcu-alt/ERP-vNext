using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TalentDevelopmentNetworkReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TalentDevelopmentNetworkReadinessState TalentDevelopmentNetworkReadinessState { get; set; }
    public TalentDevelopmentNetworkReadinessState PathwayCatalogBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState MentorshipLinkIntakeBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState ProgressionScopeBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState VisibilityControlBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState NetworkReviewBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState AutomatedDecisionBoundaryState { get; set; }
    public TalentDevelopmentNetworkReadinessState TalentDataSourceDependencyState { get; set; }
    public TalentDevelopmentNetworkReadinessState ConsentPolicyDependencyState { get; set; }
    public TalentDevelopmentNetworkReadinessState SkillPassportSourceDependencyState { get; set; }
    public TalentDevelopmentNetworkReadinessState NotificationDependencyState { get; set; }
    public TalentDevelopmentNetworkReadinessState ConsentPreconditionState { get; set; }
    public TalentDevelopmentNetworkReadinessState DataMinimizationState { get; set; }
    public TalentDevelopmentNetworkReadinessState RetentionPolicyState { get; set; }
    public TalentDevelopmentNetworkReadinessState ProgressionPolicyState { get; set; }
    public Dictionary<string, TalentDevelopmentNetworkReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long TalentDevelopmentNetworkReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
