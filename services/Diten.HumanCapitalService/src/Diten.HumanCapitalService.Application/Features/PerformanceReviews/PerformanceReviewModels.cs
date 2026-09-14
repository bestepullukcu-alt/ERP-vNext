using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews;

public class PerformanceReviewReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public PerformanceReviewReadinessState PerformanceReviewReadinessState { get; init; } = PerformanceReviewReadinessState.Draft;
    public PerformanceReviewReadinessState ReviewCycleBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState GoalDependencyState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState ScoringBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState RatingBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState CalibrationBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState RankingBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState AutomatedDecisionBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState ManagerReviewUxBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState EmployeeReviewUxBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState CompensationDataBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState BenefitsDataBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState PayrollDataBoundaryState { get; init; } = PerformanceReviewReadinessState.Blocked;
    public PerformanceReviewReadinessState DocumentDependencyState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState NotificationDependencyState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState ConsentPreconditionState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState DataMinimizationState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState RetentionPolicyState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public PerformanceReviewReadinessState EvidencePolicyState { get; init; } = PerformanceReviewReadinessState.Deferred;
    public IReadOnlyDictionary<string, PerformanceReviewReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, PerformanceReviewReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long PerformanceReviewReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record PerformanceReviewReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    PerformanceReviewReadinessState PerformanceReviewReadinessState,
    PerformanceReviewReadinessState ReviewCycleBoundaryState,
    PerformanceReviewReadinessState GoalDependencyState,
    PerformanceReviewReadinessState ScoringBoundaryState,
    PerformanceReviewReadinessState RatingBoundaryState,
    PerformanceReviewReadinessState CalibrationBoundaryState,
    PerformanceReviewReadinessState RankingBoundaryState,
    PerformanceReviewReadinessState AutomatedDecisionBoundaryState,
    PerformanceReviewReadinessState ManagerReviewUxBoundaryState,
    PerformanceReviewReadinessState EmployeeReviewUxBoundaryState,
    PerformanceReviewReadinessState CompensationDataBoundaryState,
    PerformanceReviewReadinessState BenefitsDataBoundaryState,
    PerformanceReviewReadinessState PayrollDataBoundaryState,
    PerformanceReviewReadinessState DocumentDependencyState,
    PerformanceReviewReadinessState NotificationDependencyState,
    PerformanceReviewReadinessState ConsentPreconditionState,
    PerformanceReviewReadinessState DataMinimizationState,
    PerformanceReviewReadinessState RetentionPolicyState,
    PerformanceReviewReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, PerformanceReviewReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long PerformanceReviewReadinessVersion,
    string? DeferredReason);

public sealed record PerformanceReviewReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    PerformanceReviewReadinessState PerformanceReviewReadinessState,
    PerformanceReviewReadinessState ReviewCycleBoundaryState,
    PerformanceReviewReadinessState ScoringBoundaryState,
    PerformanceReviewReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record PerformanceReviewAuditMetadataDto(
    Guid Id,
    string Code,
    PerformanceReviewReadinessState RetentionPolicyState,
    PerformanceReviewReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class PerformanceReviewMapper
{
    public static PerformanceReviewReadinessDto ToDto(PerformanceReviewReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PerformanceReviewReadinessState,
            entity.ReviewCycleBoundaryState,
            entity.GoalDependencyState,
            entity.ScoringBoundaryState,
            entity.RatingBoundaryState,
            entity.CalibrationBoundaryState,
            entity.RankingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.ManagerReviewUxBoundaryState,
            entity.EmployeeReviewUxBoundaryState,
            entity.CompensationDataBoundaryState,
            entity.BenefitsDataBoundaryState,
            entity.PayrollDataBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.PerformanceReviewReadinessVersion,
            entity.DeferredReason);

    public static PerformanceReviewReadinessListItemDto ToListItem(PerformanceReviewReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.PerformanceReviewReadinessState,
            entity.ReviewCycleBoundaryState,
            entity.ScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static PerformanceReviewAuditMetadataDto ToAuditMetadata(PerformanceReviewReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
