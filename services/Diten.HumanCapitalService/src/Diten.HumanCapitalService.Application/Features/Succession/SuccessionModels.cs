using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.Succession;

public class SuccessionReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public SuccessionReadinessState SuccessionReadinessState { get; init; } = SuccessionReadinessState.Draft;
    public SuccessionReadinessState SuccessionPoolBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState HighPotentialIdentificationBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState NominationWorkflowBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState ReadinessAssessmentBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState TalentReviewBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState AutomatedDecisionBoundaryState { get; init; } = SuccessionReadinessState.Blocked;
    public SuccessionReadinessState TalentProfileDependencyState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState PositionFrameworkDependencyState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState DocumentDependencyState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState NotificationDependencyState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState ConsentPreconditionState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState DataMinimizationState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState RetentionPolicyState { get; init; } = SuccessionReadinessState.Deferred;
    public SuccessionReadinessState EvidencePolicyState { get; init; } = SuccessionReadinessState.Deferred;
    public IReadOnlyDictionary<string, SuccessionReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, SuccessionReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long SuccessionReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record SuccessionReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    SuccessionReadinessState SuccessionReadinessState,
    SuccessionReadinessState SuccessionPoolBoundaryState,
    SuccessionReadinessState HighPotentialIdentificationBoundaryState,
    SuccessionReadinessState NominationWorkflowBoundaryState,
    SuccessionReadinessState ReadinessAssessmentBoundaryState,
    SuccessionReadinessState TalentReviewBoundaryState,
    SuccessionReadinessState AutomatedDecisionBoundaryState,
    SuccessionReadinessState TalentProfileDependencyState,
    SuccessionReadinessState PositionFrameworkDependencyState,
    SuccessionReadinessState DocumentDependencyState,
    SuccessionReadinessState NotificationDependencyState,
    SuccessionReadinessState ConsentPreconditionState,
    SuccessionReadinessState DataMinimizationState,
    SuccessionReadinessState RetentionPolicyState,
    SuccessionReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, SuccessionReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long SuccessionReadinessVersion,
    string? DeferredReason);

public sealed record SuccessionReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    SuccessionReadinessState SuccessionReadinessState,
    SuccessionReadinessState SuccessionPoolBoundaryState,
    SuccessionReadinessState TalentProfileDependencyState,
    SuccessionReadinessState ReadinessAssessmentBoundaryState,
    SuccessionReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record SuccessionAuditMetadataDto(
    Guid Id,
    string Code,
    SuccessionReadinessState RetentionPolicyState,
    SuccessionReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class SuccessionMapper
{
    public static SuccessionReadinessDto ToDto(SuccessionReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SuccessionReadinessState,
            entity.SuccessionPoolBoundaryState,
            entity.HighPotentialIdentificationBoundaryState,
            entity.NominationWorkflowBoundaryState,
            entity.ReadinessAssessmentBoundaryState,
            entity.TalentReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentProfileDependencyState,
            entity.PositionFrameworkDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.SuccessionReadinessVersion,
            entity.DeferredReason);

    public static SuccessionReadinessListItemDto ToListItem(SuccessionReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SuccessionReadinessState,
            entity.SuccessionPoolBoundaryState,
            entity.TalentProfileDependencyState,
            entity.ReadinessAssessmentBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static SuccessionAuditMetadataDto ToAuditMetadata(SuccessionReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
