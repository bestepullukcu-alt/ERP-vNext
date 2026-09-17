using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class ApplicantIntakeReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ApplicantIntakeReadinessState IntakeState { get; set; }
    public ApplicantIntakeReadinessState SourceChannelState { get; set; }
    public ApplicantIntakeReadinessState ConsentPreconditionState { get; set; }
    public ApplicantIntakeReadinessState DataMinimizationState { get; set; }
    public ApplicantIntakeReadinessState DuplicateHandlingState { get; set; }
    public ApplicantIntakeReadinessState RetentionPolicyState { get; set; }
    public ApplicantIntakeReadinessState EvidencePolicyState { get; set; }
    public ApplicantIntakeReadinessState ApplicantIdentityBoundaryState { get; set; }
    public ApplicantIntakeReadinessState PublicUxBoundaryState { get; set; }
    public ApplicantIntakeReadinessState DocumentDependencyState { get; set; }
    public ApplicantIntakeReadinessState NotificationDependencyState { get; set; }
    public Dictionary<string, ApplicantIntakeReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long ApplicantIntakeVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
