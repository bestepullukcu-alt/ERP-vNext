using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class PerformanceReviewReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PerformanceReviewReadinessState PerformanceReviewReadinessState { get; set; }
    public PerformanceReviewReadinessState ReviewCycleBoundaryState { get; set; }
    public PerformanceReviewReadinessState GoalDependencyState { get; set; }
    public PerformanceReviewReadinessState ScoringBoundaryState { get; set; }
    public PerformanceReviewReadinessState RatingBoundaryState { get; set; }
    public PerformanceReviewReadinessState CalibrationBoundaryState { get; set; }
    public PerformanceReviewReadinessState RankingBoundaryState { get; set; }
    public PerformanceReviewReadinessState AutomatedDecisionBoundaryState { get; set; }
    public PerformanceReviewReadinessState ManagerReviewUxBoundaryState { get; set; }
    public PerformanceReviewReadinessState EmployeeReviewUxBoundaryState { get; set; }
    public PerformanceReviewReadinessState CompensationDataBoundaryState { get; set; }
    public PerformanceReviewReadinessState BenefitsDataBoundaryState { get; set; }
    public PerformanceReviewReadinessState PayrollDataBoundaryState { get; set; }
    public PerformanceReviewReadinessState DocumentDependencyState { get; set; }
    public PerformanceReviewReadinessState NotificationDependencyState { get; set; }
    public PerformanceReviewReadinessState ConsentPreconditionState { get; set; }
    public PerformanceReviewReadinessState DataMinimizationState { get; set; }
    public PerformanceReviewReadinessState RetentionPolicyState { get; set; }
    public PerformanceReviewReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, PerformanceReviewReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long PerformanceReviewReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
