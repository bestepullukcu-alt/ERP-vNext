using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class CompensationBenefitsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CompensationBenefitsReadinessState CompensationBenefitsReadinessState { get; set; }
    public CompensationBenefitsReadinessState CompensationPlanBoundaryState { get; set; }
    public CompensationBenefitsReadinessState BenefitProgramBoundaryState { get; set; }
    public CompensationBenefitsReadinessState PayGradeMappingBoundaryState { get; set; }
    public CompensationBenefitsReadinessState BenefitEnrollmentBoundaryState { get; set; }
    public CompensationBenefitsReadinessState CompensationReviewBoundaryState { get; set; }
    public CompensationBenefitsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public CompensationBenefitsReadinessState CompensationSourceDependencyState { get; set; }
    public CompensationBenefitsReadinessState BenefitProviderSourceDependencyState { get; set; }
    public CompensationBenefitsReadinessState DocumentDependencyState { get; set; }
    public CompensationBenefitsReadinessState NotificationDependencyState { get; set; }
    public CompensationBenefitsReadinessState ConsentPreconditionState { get; set; }
    public CompensationBenefitsReadinessState DataMinimizationState { get; set; }
    public CompensationBenefitsReadinessState RetentionPolicyState { get; set; }
    public CompensationBenefitsReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, CompensationBenefitsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long CompensationBenefitsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
