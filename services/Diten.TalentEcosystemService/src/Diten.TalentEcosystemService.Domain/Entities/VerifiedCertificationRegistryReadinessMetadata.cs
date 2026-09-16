using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class VerifiedCertificationRegistryReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public VerifiedCertificationRegistryReadinessState VerifiedCertificationRegistryReadinessState { get; set; }
    public VerifiedCertificationRegistryReadinessState CertificationCatalogBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState VerificationIntakeBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState IssuerBindingScopeBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState VisibilityControlBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState RegistryReviewBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState AutomatedDecisionBoundaryState { get; set; }
    public VerifiedCertificationRegistryReadinessState TalentDataSourceDependencyState { get; set; }
    public VerifiedCertificationRegistryReadinessState ConsentPolicyDependencyState { get; set; }
    public VerifiedCertificationRegistryReadinessState SkillPassportSourceDependencyState { get; set; }
    public VerifiedCertificationRegistryReadinessState NotificationDependencyState { get; set; }
    public VerifiedCertificationRegistryReadinessState ConsentPreconditionState { get; set; }
    public VerifiedCertificationRegistryReadinessState DataMinimizationState { get; set; }
    public VerifiedCertificationRegistryReadinessState RetentionPolicyState { get; set; }
    public VerifiedCertificationRegistryReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, VerifiedCertificationRegistryReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long VerifiedCertificationRegistryReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
