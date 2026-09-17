using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits;

public class CompensationBenefitsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public CompensationBenefitsReadinessState CompensationBenefitsReadinessState { get; init; } = CompensationBenefitsReadinessState.Draft;
    public CompensationBenefitsReadinessState CompensationPlanBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState BenefitProgramBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState PayGradeMappingBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState BenefitEnrollmentBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState CompensationReviewBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState AutomatedDecisionBoundaryState { get; init; } = CompensationBenefitsReadinessState.Blocked;
    public CompensationBenefitsReadinessState CompensationSourceDependencyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState BenefitProviderSourceDependencyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState DocumentDependencyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState NotificationDependencyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState ConsentPreconditionState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState DataMinimizationState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState RetentionPolicyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public CompensationBenefitsReadinessState EvidencePolicyState { get; init; } = CompensationBenefitsReadinessState.Deferred;
    public IReadOnlyDictionary<string, CompensationBenefitsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, CompensationBenefitsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long CompensationBenefitsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record CompensationBenefitsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    CompensationBenefitsReadinessState CompensationBenefitsReadinessState,
    CompensationBenefitsReadinessState CompensationPlanBoundaryState,
    CompensationBenefitsReadinessState BenefitProgramBoundaryState,
    CompensationBenefitsReadinessState PayGradeMappingBoundaryState,
    CompensationBenefitsReadinessState BenefitEnrollmentBoundaryState,
    CompensationBenefitsReadinessState CompensationReviewBoundaryState,
    CompensationBenefitsReadinessState AutomatedDecisionBoundaryState,
    CompensationBenefitsReadinessState CompensationSourceDependencyState,
    CompensationBenefitsReadinessState BenefitProviderSourceDependencyState,
    CompensationBenefitsReadinessState DocumentDependencyState,
    CompensationBenefitsReadinessState NotificationDependencyState,
    CompensationBenefitsReadinessState ConsentPreconditionState,
    CompensationBenefitsReadinessState DataMinimizationState,
    CompensationBenefitsReadinessState RetentionPolicyState,
    CompensationBenefitsReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, CompensationBenefitsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long CompensationBenefitsReadinessVersion,
    string? DeferredReason);

public sealed record CompensationBenefitsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    CompensationBenefitsReadinessState CompensationBenefitsReadinessState,
    CompensationBenefitsReadinessState CompensationPlanBoundaryState,
    CompensationBenefitsReadinessState CompensationSourceDependencyState,
    CompensationBenefitsReadinessState BenefitEnrollmentBoundaryState,
    CompensationBenefitsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record CompensationBenefitsAuditMetadataDto(
    Guid Id,
    string Code,
    CompensationBenefitsReadinessState RetentionPolicyState,
    CompensationBenefitsReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class CompensationBenefitsMapper
{
    public static CompensationBenefitsReadinessDto ToDto(CompensationBenefitsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CompensationBenefitsReadinessState,
            entity.CompensationPlanBoundaryState,
            entity.BenefitProgramBoundaryState,
            entity.PayGradeMappingBoundaryState,
            entity.BenefitEnrollmentBoundaryState,
            entity.CompensationReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.CompensationSourceDependencyState,
            entity.BenefitProviderSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.CompensationBenefitsReadinessVersion,
            entity.DeferredReason);

    public static CompensationBenefitsReadinessListItemDto ToListItem(CompensationBenefitsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.CompensationBenefitsReadinessState,
            entity.CompensationPlanBoundaryState,
            entity.CompensationSourceDependencyState,
            entity.BenefitEnrollmentBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static CompensationBenefitsAuditMetadataDto ToAuditMetadata(CompensationBenefitsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
