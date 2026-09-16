using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class IndustrySkillPassportReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IndustrySkillPassportReadinessState IndustrySkillPassportReadinessState { get; set; }
    public IndustrySkillPassportReadinessState SkillClaimCatalogBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState AttestationIntakeBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState VerificationScopeBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState VisibilityControlBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState PassportReviewBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState AutomatedDecisionBoundaryState { get; set; }
    public IndustrySkillPassportReadinessState TalentDataSourceDependencyState { get; set; }
    public IndustrySkillPassportReadinessState ConsentPolicyDependencyState { get; set; }
    public IndustrySkillPassportReadinessState CertificationSourceDependencyState { get; set; }
    public IndustrySkillPassportReadinessState NotificationDependencyState { get; set; }
    public IndustrySkillPassportReadinessState ConsentPreconditionState { get; set; }
    public IndustrySkillPassportReadinessState DataMinimizationState { get; set; }
    public IndustrySkillPassportReadinessState RetentionPolicyState { get; set; }
    public IndustrySkillPassportReadinessState VerificationPolicyState { get; set; }
    public Dictionary<string, IndustrySkillPassportReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long IndustrySkillPassportReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
