using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.HiringRiskIndicators;

public class HiringRiskIndicatorsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HiringRiskIndicatorsReadinessState HiringRiskIndicatorsReadinessState { get; init; } = HiringRiskIndicatorsReadinessState.Draft;
    public HiringRiskIndicatorsReadinessState RiskIndicatorCatalogBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState RiskSignalIntakeBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState RiskAssessmentBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState MitigationTrackingBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState IndicatorReviewBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState AutomatedDecisionBoundaryState { get; init; } = HiringRiskIndicatorsReadinessState.Blocked;
    public HiringRiskIndicatorsReadinessState TalentDataSourceDependencyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState ConsentPolicyDependencyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState DocumentDependencyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState NotificationDependencyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState ConsentPreconditionState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState DataMinimizationState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState RetentionPolicyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public HiringRiskIndicatorsReadinessState EvidencePolicyState { get; init; } = HiringRiskIndicatorsReadinessState.Deferred;
    public IReadOnlyDictionary<string, HiringRiskIndicatorsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HiringRiskIndicatorsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HiringRiskIndicatorsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HiringRiskIndicatorsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HiringRiskIndicatorsReadinessState HiringRiskIndicatorsReadinessState,
    HiringRiskIndicatorsReadinessState RiskIndicatorCatalogBoundaryState,
    HiringRiskIndicatorsReadinessState RiskSignalIntakeBoundaryState,
    HiringRiskIndicatorsReadinessState RiskAssessmentBoundaryState,
    HiringRiskIndicatorsReadinessState MitigationTrackingBoundaryState,
    HiringRiskIndicatorsReadinessState IndicatorReviewBoundaryState,
    HiringRiskIndicatorsReadinessState AutomatedDecisionBoundaryState,
    HiringRiskIndicatorsReadinessState TalentDataSourceDependencyState,
    HiringRiskIndicatorsReadinessState ConsentPolicyDependencyState,
    HiringRiskIndicatorsReadinessState DocumentDependencyState,
    HiringRiskIndicatorsReadinessState NotificationDependencyState,
    HiringRiskIndicatorsReadinessState ConsentPreconditionState,
    HiringRiskIndicatorsReadinessState DataMinimizationState,
    HiringRiskIndicatorsReadinessState RetentionPolicyState,
    HiringRiskIndicatorsReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HiringRiskIndicatorsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HiringRiskIndicatorsReadinessVersion,
    string? DeferredReason);

public sealed record HiringRiskIndicatorsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HiringRiskIndicatorsReadinessState HiringRiskIndicatorsReadinessState,
    HiringRiskIndicatorsReadinessState RiskIndicatorCatalogBoundaryState,
    HiringRiskIndicatorsReadinessState TalentDataSourceDependencyState,
    HiringRiskIndicatorsReadinessState MitigationTrackingBoundaryState,
    HiringRiskIndicatorsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HiringRiskIndicatorsAuditMetadataDto(
    Guid Id,
    string Code,
    HiringRiskIndicatorsReadinessState RetentionPolicyState,
    HiringRiskIndicatorsReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HiringRiskIndicatorsMapper
{
    public static HiringRiskIndicatorsReadinessDto ToDto(HiringRiskIndicatorsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HiringRiskIndicatorsReadinessState,
            entity.RiskIndicatorCatalogBoundaryState,
            entity.RiskSignalIntakeBoundaryState,
            entity.RiskAssessmentBoundaryState,
            entity.MitigationTrackingBoundaryState,
            entity.IndicatorReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HiringRiskIndicatorsReadinessVersion,
            entity.DeferredReason);

    public static HiringRiskIndicatorsReadinessListItemDto ToListItem(HiringRiskIndicatorsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HiringRiskIndicatorsReadinessState,
            entity.RiskIndicatorCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.MitigationTrackingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HiringRiskIndicatorsAuditMetadataDto ToAuditMetadata(HiringRiskIndicatorsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
