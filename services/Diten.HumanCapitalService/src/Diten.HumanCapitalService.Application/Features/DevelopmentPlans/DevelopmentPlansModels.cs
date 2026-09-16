using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.DevelopmentPlans;

public class DevelopmentPlanReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DevelopmentPlanReadinessState DevelopmentPlanReadinessState { get; init; } = DevelopmentPlanReadinessState.Draft;
    public DevelopmentPlanReadinessState DevelopmentPlanWorkflowBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState GoalAssignmentBoundaryState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState LearningAssignmentBoundaryState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState SkillGapScoringBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState RatingBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState RecommendationBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState RankingBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState AutomatedDecisionBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState ManagerActionUxBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState CoachingActionBoundaryState { get; init; } = DevelopmentPlanReadinessState.Blocked;
    public DevelopmentPlanReadinessState DocumentDependencyState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState NotificationDependencyState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState ConsentPreconditionState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState DataMinimizationState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState RetentionPolicyState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public DevelopmentPlanReadinessState EvidencePolicyState { get; init; } = DevelopmentPlanReadinessState.Deferred;
    public IReadOnlyDictionary<string, DevelopmentPlanReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, DevelopmentPlanReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long DevelopmentPlanReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record DevelopmentPlanReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    DevelopmentPlanReadinessState DevelopmentPlanReadinessState,
    DevelopmentPlanReadinessState DevelopmentPlanWorkflowBoundaryState,
    DevelopmentPlanReadinessState GoalAssignmentBoundaryState,
    DevelopmentPlanReadinessState LearningAssignmentBoundaryState,
    DevelopmentPlanReadinessState SkillGapScoringBoundaryState,
    DevelopmentPlanReadinessState RatingBoundaryState,
    DevelopmentPlanReadinessState RecommendationBoundaryState,
    DevelopmentPlanReadinessState RankingBoundaryState,
    DevelopmentPlanReadinessState AutomatedDecisionBoundaryState,
    DevelopmentPlanReadinessState ManagerActionUxBoundaryState,
    DevelopmentPlanReadinessState CoachingActionBoundaryState,
    DevelopmentPlanReadinessState DocumentDependencyState,
    DevelopmentPlanReadinessState NotificationDependencyState,
    DevelopmentPlanReadinessState ConsentPreconditionState,
    DevelopmentPlanReadinessState DataMinimizationState,
    DevelopmentPlanReadinessState RetentionPolicyState,
    DevelopmentPlanReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, DevelopmentPlanReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long DevelopmentPlanReadinessVersion,
    string? DeferredReason);

public sealed record DevelopmentPlanReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    DevelopmentPlanReadinessState DevelopmentPlanReadinessState,
    DevelopmentPlanReadinessState DevelopmentPlanWorkflowBoundaryState,
    DevelopmentPlanReadinessState LearningAssignmentBoundaryState,
    DevelopmentPlanReadinessState SkillGapScoringBoundaryState,
    DevelopmentPlanReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record DevelopmentPlanAuditMetadataDto(
    Guid Id,
    string Code,
    DevelopmentPlanReadinessState RetentionPolicyState,
    DevelopmentPlanReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class DevelopmentPlanMapper
{
    public static DevelopmentPlanReadinessDto ToDto(DevelopmentPlanReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DevelopmentPlanReadinessState,
            entity.DevelopmentPlanWorkflowBoundaryState,
            entity.GoalAssignmentBoundaryState,
            entity.LearningAssignmentBoundaryState,
            entity.SkillGapScoringBoundaryState,
            entity.RatingBoundaryState,
            entity.RecommendationBoundaryState,
            entity.RankingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.ManagerActionUxBoundaryState,
            entity.CoachingActionBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.DevelopmentPlanReadinessVersion,
            entity.DeferredReason);

    public static DevelopmentPlanReadinessListItemDto ToListItem(DevelopmentPlanReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.DevelopmentPlanReadinessState,
            entity.DevelopmentPlanWorkflowBoundaryState,
            entity.LearningAssignmentBoundaryState,
            entity.SkillGapScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static DevelopmentPlanAuditMetadataDto ToAuditMetadata(DevelopmentPlanReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
