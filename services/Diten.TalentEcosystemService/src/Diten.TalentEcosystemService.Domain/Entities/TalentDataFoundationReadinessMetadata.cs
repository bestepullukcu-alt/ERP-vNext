using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TalentDataFoundationReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TalentDataFoundationReadinessState TalentDataFoundationReadinessState { get; set; }
    public TalentDataFoundationReadinessState TalentEntityCatalogBoundaryState { get; set; }
    public TalentDataFoundationReadinessState DataIngestionBoundaryState { get; set; }
    public TalentDataFoundationReadinessState IdentityResolutionBoundaryState { get; set; }
    public TalentDataFoundationReadinessState DataQualityBoundaryState { get; set; }
    public TalentDataFoundationReadinessState LineageTrackingBoundaryState { get; set; }
    public TalentDataFoundationReadinessState AutomatedDecisionBoundaryState { get; set; }
    public TalentDataFoundationReadinessState HcmFoundationDependencyState { get; set; }
    public TalentDataFoundationReadinessState ConsentPolicyDependencyState { get; set; }
    public TalentDataFoundationReadinessState DocumentDependencyState { get; set; }
    public TalentDataFoundationReadinessState NotificationDependencyState { get; set; }
    public TalentDataFoundationReadinessState ConsentPreconditionState { get; set; }
    public TalentDataFoundationReadinessState DataMinimizationState { get; set; }
    public TalentDataFoundationReadinessState RetentionPolicyState { get; set; }
    public TalentDataFoundationReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, TalentDataFoundationReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long TalentDataFoundationReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
