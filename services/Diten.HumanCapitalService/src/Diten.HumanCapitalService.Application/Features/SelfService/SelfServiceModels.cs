using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.SelfService;

public class SelfServiceReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public SelfServiceReadinessState SelfServiceReadinessState { get; init; } = SelfServiceReadinessState.Draft;
    public SelfServiceReadinessState RequestIntakeBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState ApprovalRoutingBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState InboxDeliveryBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState ProfileSelfUpdateBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState DelegationScopeBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState AutomatedDecisionBoundaryState { get; init; } = SelfServiceReadinessState.Blocked;
    public SelfServiceReadinessState IdentityDirectoryDependencyState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState HcmCapabilityDependencyState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState DocumentDependencyState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState NotificationDependencyState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState ConsentPreconditionState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState DataMinimizationState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState RetentionPolicyState { get; init; } = SelfServiceReadinessState.Deferred;
    public SelfServiceReadinessState EvidencePolicyState { get; init; } = SelfServiceReadinessState.Deferred;
    public IReadOnlyDictionary<string, SelfServiceReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, SelfServiceReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long SelfServiceReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record SelfServiceReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    SelfServiceReadinessState SelfServiceReadinessState,
    SelfServiceReadinessState RequestIntakeBoundaryState,
    SelfServiceReadinessState ApprovalRoutingBoundaryState,
    SelfServiceReadinessState InboxDeliveryBoundaryState,
    SelfServiceReadinessState ProfileSelfUpdateBoundaryState,
    SelfServiceReadinessState DelegationScopeBoundaryState,
    SelfServiceReadinessState AutomatedDecisionBoundaryState,
    SelfServiceReadinessState IdentityDirectoryDependencyState,
    SelfServiceReadinessState HcmCapabilityDependencyState,
    SelfServiceReadinessState DocumentDependencyState,
    SelfServiceReadinessState NotificationDependencyState,
    SelfServiceReadinessState ConsentPreconditionState,
    SelfServiceReadinessState DataMinimizationState,
    SelfServiceReadinessState RetentionPolicyState,
    SelfServiceReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, SelfServiceReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long SelfServiceReadinessVersion,
    string? DeferredReason);

public sealed record SelfServiceReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    SelfServiceReadinessState SelfServiceReadinessState,
    SelfServiceReadinessState RequestIntakeBoundaryState,
    SelfServiceReadinessState IdentityDirectoryDependencyState,
    SelfServiceReadinessState ProfileSelfUpdateBoundaryState,
    SelfServiceReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record SelfServiceAuditMetadataDto(
    Guid Id,
    string Code,
    SelfServiceReadinessState RetentionPolicyState,
    SelfServiceReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class SelfServiceMapper
{
    public static SelfServiceReadinessDto ToDto(SelfServiceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SelfServiceReadinessState,
            entity.RequestIntakeBoundaryState,
            entity.ApprovalRoutingBoundaryState,
            entity.InboxDeliveryBoundaryState,
            entity.ProfileSelfUpdateBoundaryState,
            entity.DelegationScopeBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.IdentityDirectoryDependencyState,
            entity.HcmCapabilityDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.SelfServiceReadinessVersion,
            entity.DeferredReason);

    public static SelfServiceReadinessListItemDto ToListItem(SelfServiceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.SelfServiceReadinessState,
            entity.RequestIntakeBoundaryState,
            entity.IdentityDirectoryDependencyState,
            entity.ProfileSelfUpdateBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static SelfServiceAuditMetadataDto ToAuditMetadata(SelfServiceReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
