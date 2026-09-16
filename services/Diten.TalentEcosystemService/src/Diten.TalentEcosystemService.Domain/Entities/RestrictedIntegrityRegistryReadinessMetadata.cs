using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class RestrictedIntegrityRegistryReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public RestrictedIntegrityRegistryReadinessState RestrictedIntegrityRegistryReadinessState { get; set; }
    public RestrictedIntegrityRegistryReadinessState IntegrityCaseCatalogBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState RestrictionScopeBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState EvidenceChainBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState DisclosureControlBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState CaseReviewBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState AutomatedDecisionBoundaryState { get; set; }
    public RestrictedIntegrityRegistryReadinessState EarlyWarningSourceDependencyState { get; set; }
    public RestrictedIntegrityRegistryReadinessState ConsentPolicyDependencyState { get; set; }
    public RestrictedIntegrityRegistryReadinessState LegalHoldDependencyState { get; set; }
    public RestrictedIntegrityRegistryReadinessState NotificationDependencyState { get; set; }
    public RestrictedIntegrityRegistryReadinessState ConsentPreconditionState { get; set; }
    public RestrictedIntegrityRegistryReadinessState DataMinimizationState { get; set; }
    public RestrictedIntegrityRegistryReadinessState RetentionPolicyState { get; set; }
    public RestrictedIntegrityRegistryReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, RestrictedIntegrityRegistryReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long RestrictedIntegrityRegistryReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
