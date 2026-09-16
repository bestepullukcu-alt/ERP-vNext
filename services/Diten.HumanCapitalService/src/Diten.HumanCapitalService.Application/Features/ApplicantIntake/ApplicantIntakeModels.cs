using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake;

public class ApplicantIntakeCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ApplicantIntakeReadinessState IntakeState { get; init; } = ApplicantIntakeReadinessState.Draft;
    public ApplicantIntakeReadinessState SourceChannelState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState ConsentPreconditionState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState DataMinimizationState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState DuplicateHandlingState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState RetentionPolicyState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState EvidencePolicyState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState ApplicantIdentityBoundaryState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState PublicUxBoundaryState { get; init; } = ApplicantIntakeReadinessState.Blocked;
    public ApplicantIntakeReadinessState DocumentDependencyState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public ApplicantIntakeReadinessState NotificationDependencyState { get; init; } = ApplicantIntakeReadinessState.Deferred;
    public IReadOnlyDictionary<string, ApplicantIntakeReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, ApplicantIntakeReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long ApplicantIntakeVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record ApplicantIntakeReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    ApplicantIntakeReadinessState IntakeState,
    ApplicantIntakeReadinessState SourceChannelState,
    ApplicantIntakeReadinessState ConsentPreconditionState,
    ApplicantIntakeReadinessState DataMinimizationState,
    ApplicantIntakeReadinessState DuplicateHandlingState,
    ApplicantIntakeReadinessState RetentionPolicyState,
    ApplicantIntakeReadinessState EvidencePolicyState,
    ApplicantIntakeReadinessState ApplicantIdentityBoundaryState,
    ApplicantIntakeReadinessState PublicUxBoundaryState,
    ApplicantIntakeReadinessState DocumentDependencyState,
    ApplicantIntakeReadinessState NotificationDependencyState,
    IReadOnlyDictionary<string, ApplicantIntakeReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long ApplicantIntakeVersion,
    string? DeferredReason);

public sealed record ApplicantIntakeReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    ApplicantIntakeReadinessState IntakeState,
    ApplicantIntakeReadinessState SourceChannelState,
    ApplicantIntakeReadinessState ConsentPreconditionState,
    ApplicantIntakeReadinessState DataMinimizationState,
    ApplicantIntakeReadinessState DuplicateHandlingState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record ApplicantIntakeAuditMetadataDto(
    Guid Id,
    string Code,
    ApplicantIntakeReadinessState RetentionPolicyState,
    ApplicantIntakeReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class ApplicantIntakeMapper
{
    public static ApplicantIntakeReadinessDto ToDto(ApplicantIntakeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IntakeState,
            entity.SourceChannelState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.DuplicateHandlingState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.ApplicantIdentityBoundaryState,
            entity.PublicUxBoundaryState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.ApplicantIntakeVersion,
            entity.DeferredReason);

    public static ApplicantIntakeReadinessListItemDto ToListItem(ApplicantIntakeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.IntakeState,
            entity.SourceChannelState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.DuplicateHandlingState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static ApplicantIntakeAuditMetadataDto ToAuditMetadata(ApplicantIntakeReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
