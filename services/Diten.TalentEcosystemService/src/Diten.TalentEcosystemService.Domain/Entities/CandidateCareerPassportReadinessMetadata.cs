using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class CandidateCareerPassportReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CandidateCareerPassportReadinessState CandidateCareerPassportReadinessState { get; set; }
    public CandidateCareerPassportReadinessState CareerMilestoneCatalogBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState ExperienceIntakeBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState OwnershipScopeBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState VisibilityControlBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState PassportReviewBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState AutomatedDecisionBoundaryState { get; set; }
    public CandidateCareerPassportReadinessState TalentDataSourceDependencyState { get; set; }
    public CandidateCareerPassportReadinessState ConsentPolicyDependencyState { get; set; }
    public CandidateCareerPassportReadinessState SkillPassportSourceDependencyState { get; set; }
    public CandidateCareerPassportReadinessState NotificationDependencyState { get; set; }
    public CandidateCareerPassportReadinessState ConsentPreconditionState { get; set; }
    public CandidateCareerPassportReadinessState DataMinimizationState { get; set; }
    public CandidateCareerPassportReadinessState RetentionPolicyState { get; set; }
    public CandidateCareerPassportReadinessState PortabilityPolicyState { get; set; }
    public Dictionary<string, CandidateCareerPassportReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long CandidateCareerPassportReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
