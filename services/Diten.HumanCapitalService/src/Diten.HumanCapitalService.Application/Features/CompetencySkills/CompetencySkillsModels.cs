using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.CompetencySkills;

public class CompetencySkillsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CompetencySkillsReadinessState CompetencySkillsReadinessState { get; init; } = CompetencySkillsReadinessState.Draft;
    public CompetencySkillsReadinessState AssessmentWorkflowBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState CompetencyFrameworkDependencyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState SkillTaxonomyDependencyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState SkillScoringBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState RatingBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState CalibrationBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState RankingBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState AutomatedDecisionBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState ManagerAssessmentUxBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState EmployeeAssessmentUxBoundaryState { get; init; } = CompetencySkillsReadinessState.Blocked;
    public CompetencySkillsReadinessState DocumentDependencyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState NotificationDependencyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState ConsentPreconditionState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState DataMinimizationState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState RetentionPolicyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public CompetencySkillsReadinessState EvidencePolicyState { get; init; } = CompetencySkillsReadinessState.Deferred;
    public IReadOnlyDictionary<string, CompetencySkillsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, CompetencySkillsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long CompetencySkillsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record CompetencySkillsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    CompetencySkillsReadinessState CompetencySkillsReadinessState,
    CompetencySkillsReadinessState AssessmentWorkflowBoundaryState,
    CompetencySkillsReadinessState CompetencyFrameworkDependencyState,
    CompetencySkillsReadinessState SkillTaxonomyDependencyState,
    CompetencySkillsReadinessState SkillScoringBoundaryState,
    CompetencySkillsReadinessState RatingBoundaryState,
    CompetencySkillsReadinessState CalibrationBoundaryState,
    CompetencySkillsReadinessState RankingBoundaryState,
    CompetencySkillsReadinessState AutomatedDecisionBoundaryState,
    CompetencySkillsReadinessState ManagerAssessmentUxBoundaryState,
    CompetencySkillsReadinessState EmployeeAssessmentUxBoundaryState,
    CompetencySkillsReadinessState DocumentDependencyState,
    CompetencySkillsReadinessState NotificationDependencyState,
    CompetencySkillsReadinessState ConsentPreconditionState,
    CompetencySkillsReadinessState DataMinimizationState,
    CompetencySkillsReadinessState RetentionPolicyState,
    CompetencySkillsReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, CompetencySkillsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long CompetencySkillsReadinessVersion,
    string? DeferredReason);

public sealed record CompetencySkillsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    CompetencySkillsReadinessState CompetencySkillsReadinessState,
    CompetencySkillsReadinessState AssessmentWorkflowBoundaryState,
    CompetencySkillsReadinessState SkillTaxonomyDependencyState,
    CompetencySkillsReadinessState SkillScoringBoundaryState,
    CompetencySkillsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record CompetencySkillsAuditMetadataDto(
    Guid Id,
    string Code,
    CompetencySkillsReadinessState RetentionPolicyState,
    CompetencySkillsReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class CompetencySkillsMapper
{
    public static CompetencySkillsReadinessDto ToDto(CompetencySkillsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CompetencySkillsReadinessState,
            entity.AssessmentWorkflowBoundaryState,
            entity.CompetencyFrameworkDependencyState,
            entity.SkillTaxonomyDependencyState,
            entity.SkillScoringBoundaryState,
            entity.RatingBoundaryState,
            entity.CalibrationBoundaryState,
            entity.RankingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.ManagerAssessmentUxBoundaryState,
            entity.EmployeeAssessmentUxBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.CompetencySkillsReadinessVersion,
            entity.DeferredReason);

    public static CompetencySkillsReadinessListItemDto ToListItem(CompetencySkillsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CompetencySkillsReadinessState,
            entity.AssessmentWorkflowBoundaryState,
            entity.SkillTaxonomyDependencyState,
            entity.SkillScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static CompetencySkillsAuditMetadataDto ToAuditMetadata(CompetencySkillsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
