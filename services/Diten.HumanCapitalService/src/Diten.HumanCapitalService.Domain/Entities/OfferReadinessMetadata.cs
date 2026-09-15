using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class OfferReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public OfferReadinessState OfferReadinessState { get; set; }
    public OfferReadinessState OfferWorkflowBoundaryState { get; set; }
    public OfferReadinessState ApprovalWorkflowBoundaryState { get; set; }
    public OfferReadinessState CandidateAcceptanceBoundaryState { get; set; }
    public OfferReadinessState OfferDocumentBoundaryState { get; set; }
    public OfferReadinessState CompensationDataBoundaryState { get; set; }
    public OfferReadinessState BenefitsDataBoundaryState { get; set; }
    public OfferReadinessState PayrollDataBoundaryState { get; set; }
    public OfferReadinessState ConsentPreconditionState { get; set; }
    public OfferReadinessState DataMinimizationState { get; set; }
    public OfferReadinessState RetentionPolicyState { get; set; }
    public OfferReadinessState EvidencePolicyState { get; set; }
    public OfferReadinessState NotificationDependencyState { get; set; }
    public OfferReadinessState DocumentDependencyState { get; set; }
    public Dictionary<string, OfferReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long OfferReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
