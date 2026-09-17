using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining;

public class LearningTrainingReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public LearningTrainingReadinessState LearningTrainingReadinessState { get; init; } = LearningTrainingReadinessState.Draft;
    public LearningTrainingReadinessState CourseCatalogBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState EnrollmentWorkflowBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState CompletionTrackingBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState CertificationBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState AssessmentScoringBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState AutomatedDecisionBoundaryState { get; init; } = LearningTrainingReadinessState.Blocked;
    public LearningTrainingReadinessState LearningContentDependencyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState SkillTaxonomyDependencyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState DocumentDependencyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState NotificationDependencyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState ConsentPreconditionState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState DataMinimizationState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState RetentionPolicyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public LearningTrainingReadinessState EvidencePolicyState { get; init; } = LearningTrainingReadinessState.Deferred;
    public IReadOnlyDictionary<string, LearningTrainingReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, LearningTrainingReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long LearningTrainingReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record LearningTrainingReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    LearningTrainingReadinessState LearningTrainingReadinessState,
    LearningTrainingReadinessState CourseCatalogBoundaryState,
    LearningTrainingReadinessState EnrollmentWorkflowBoundaryState,
    LearningTrainingReadinessState CompletionTrackingBoundaryState,
    LearningTrainingReadinessState CertificationBoundaryState,
    LearningTrainingReadinessState AssessmentScoringBoundaryState,
    LearningTrainingReadinessState AutomatedDecisionBoundaryState,
    LearningTrainingReadinessState LearningContentDependencyState,
    LearningTrainingReadinessState SkillTaxonomyDependencyState,
    LearningTrainingReadinessState DocumentDependencyState,
    LearningTrainingReadinessState NotificationDependencyState,
    LearningTrainingReadinessState ConsentPreconditionState,
    LearningTrainingReadinessState DataMinimizationState,
    LearningTrainingReadinessState RetentionPolicyState,
    LearningTrainingReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, LearningTrainingReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long LearningTrainingReadinessVersion,
    string? DeferredReason);

public sealed record LearningTrainingReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    LearningTrainingReadinessState LearningTrainingReadinessState,
    LearningTrainingReadinessState CourseCatalogBoundaryState,
    LearningTrainingReadinessState SkillTaxonomyDependencyState,
    LearningTrainingReadinessState AssessmentScoringBoundaryState,
    LearningTrainingReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record LearningTrainingAuditMetadataDto(
    Guid Id,
    string Code,
    LearningTrainingReadinessState RetentionPolicyState,
    LearningTrainingReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class LearningTrainingMapper
{
    public static LearningTrainingReadinessDto ToDto(LearningTrainingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.LearningTrainingReadinessState,
            entity.CourseCatalogBoundaryState,
            entity.EnrollmentWorkflowBoundaryState,
            entity.CompletionTrackingBoundaryState,
            entity.CertificationBoundaryState,
            entity.AssessmentScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.LearningContentDependencyState,
            entity.SkillTaxonomyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.LearningTrainingReadinessVersion,
            entity.DeferredReason);

    public static LearningTrainingReadinessListItemDto ToListItem(LearningTrainingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.LearningTrainingReadinessState,
            entity.CourseCatalogBoundaryState,
            entity.SkillTaxonomyDependencyState,
            entity.AssessmentScoringBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static LearningTrainingAuditMetadataDto ToAuditMetadata(LearningTrainingReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
