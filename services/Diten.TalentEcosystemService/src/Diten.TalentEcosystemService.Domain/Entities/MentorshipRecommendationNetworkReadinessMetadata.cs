using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class MentorshipRecommendationNetworkReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MentorshipRecommendationNetworkReadinessState MentorshipRecommendationNetworkReadinessState { get; set; }
    public MentorshipRecommendationNetworkReadinessState NetworkCatalogBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState PairingIntakeBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState RecommendationScopeBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState VisibilityControlBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState NetworkReviewBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState AutomatedDecisionBoundaryState { get; set; }
    public MentorshipRecommendationNetworkReadinessState TalentDataSourceDependencyState { get; set; }
    public MentorshipRecommendationNetworkReadinessState ConsentPolicyDependencyState { get; set; }
    public MentorshipRecommendationNetworkReadinessState ReputationSourceDependencyState { get; set; }
    public MentorshipRecommendationNetworkReadinessState NotificationDependencyState { get; set; }
    public MentorshipRecommendationNetworkReadinessState ConsentPreconditionState { get; set; }
    public MentorshipRecommendationNetworkReadinessState DataMinimizationState { get; set; }
    public MentorshipRecommendationNetworkReadinessState RetentionPolicyState { get; set; }
    public MentorshipRecommendationNetworkReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, MentorshipRecommendationNetworkReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long MentorshipRecommendationNetworkReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
