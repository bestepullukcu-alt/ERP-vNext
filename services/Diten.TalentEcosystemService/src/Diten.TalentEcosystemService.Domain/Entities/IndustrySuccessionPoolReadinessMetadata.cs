using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class IndustrySuccessionPoolReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IndustrySuccessionPoolReadinessState IndustrySuccessionPoolReadinessState { get; set; }
    public IndustrySuccessionPoolReadinessState PoolCatalogBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState CandidateInclusionIntakeBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState ReadinessTierScopeBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState VisibilityControlBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState SuccessionReviewBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState AutomatedDecisionBoundaryState { get; set; }
    public IndustrySuccessionPoolReadinessState TalentDataSourceDependencyState { get; set; }
    public IndustrySuccessionPoolReadinessState ConsentPolicyDependencyState { get; set; }
    public IndustrySuccessionPoolReadinessState TalentPoolSourceDependencyState { get; set; }
    public IndustrySuccessionPoolReadinessState NotificationDependencyState { get; set; }
    public IndustrySuccessionPoolReadinessState ConsentPreconditionState { get; set; }
    public IndustrySuccessionPoolReadinessState DataMinimizationState { get; set; }
    public IndustrySuccessionPoolReadinessState RetentionPolicyState { get; set; }
    public IndustrySuccessionPoolReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, IndustrySuccessionPoolReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long IndustrySuccessionPoolReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
