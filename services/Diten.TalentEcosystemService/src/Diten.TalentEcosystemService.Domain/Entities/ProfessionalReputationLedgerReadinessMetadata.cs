using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class ProfessionalReputationLedgerReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProfessionalReputationLedgerReadinessState ProfessionalReputationLedgerReadinessState { get; set; }
    public ProfessionalReputationLedgerReadinessState ReputationSignalCatalogBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState EndorsementIntakeBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState AttributionScopeBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState VisibilityControlBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState SignalReviewBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState AutomatedDecisionBoundaryState { get; set; }
    public ProfessionalReputationLedgerReadinessState TalentDataSourceDependencyState { get; set; }
    public ProfessionalReputationLedgerReadinessState ConsentPolicyDependencyState { get; set; }
    public ProfessionalReputationLedgerReadinessState DocumentDependencyState { get; set; }
    public ProfessionalReputationLedgerReadinessState NotificationDependencyState { get; set; }
    public ProfessionalReputationLedgerReadinessState ConsentPreconditionState { get; set; }
    public ProfessionalReputationLedgerReadinessState DataMinimizationState { get; set; }
    public ProfessionalReputationLedgerReadinessState RetentionPolicyState { get; set; }
    public ProfessionalReputationLedgerReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, ProfessionalReputationLedgerReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long ProfessionalReputationLedgerReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
