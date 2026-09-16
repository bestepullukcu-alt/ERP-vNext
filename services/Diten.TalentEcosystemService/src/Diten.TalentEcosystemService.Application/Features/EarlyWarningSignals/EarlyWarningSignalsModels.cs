using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals;

public class EarlyWarningSignalsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public EarlyWarningSignalsReadinessState EarlyWarningSignalsReadinessState { get; init; } = EarlyWarningSignalsReadinessState.Draft;
    public EarlyWarningSignalsReadinessState SignalCatalogBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState PatternDetectionBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState CrossCompanyCorrelationBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState AlertRoutingBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState SignalReviewBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState AutomatedDecisionBoundaryState { get; init; } = EarlyWarningSignalsReadinessState.Blocked;
    public EarlyWarningSignalsReadinessState TalentDataSourceDependencyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState RiskIndicatorSourceDependencyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState DocumentDependencyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState NotificationDependencyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState ConsentPreconditionState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState DataMinimizationState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState RetentionPolicyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public EarlyWarningSignalsReadinessState EvidencePolicyState { get; init; } = EarlyWarningSignalsReadinessState.Deferred;
    public IReadOnlyDictionary<string, EarlyWarningSignalsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, EarlyWarningSignalsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long EarlyWarningSignalsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record EarlyWarningSignalsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    EarlyWarningSignalsReadinessState EarlyWarningSignalsReadinessState,
    EarlyWarningSignalsReadinessState SignalCatalogBoundaryState,
    EarlyWarningSignalsReadinessState PatternDetectionBoundaryState,
    EarlyWarningSignalsReadinessState CrossCompanyCorrelationBoundaryState,
    EarlyWarningSignalsReadinessState AlertRoutingBoundaryState,
    EarlyWarningSignalsReadinessState SignalReviewBoundaryState,
    EarlyWarningSignalsReadinessState AutomatedDecisionBoundaryState,
    EarlyWarningSignalsReadinessState TalentDataSourceDependencyState,
    EarlyWarningSignalsReadinessState RiskIndicatorSourceDependencyState,
    EarlyWarningSignalsReadinessState DocumentDependencyState,
    EarlyWarningSignalsReadinessState NotificationDependencyState,
    EarlyWarningSignalsReadinessState ConsentPreconditionState,
    EarlyWarningSignalsReadinessState DataMinimizationState,
    EarlyWarningSignalsReadinessState RetentionPolicyState,
    EarlyWarningSignalsReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, EarlyWarningSignalsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long EarlyWarningSignalsReadinessVersion,
    string? DeferredReason);

public sealed record EarlyWarningSignalsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    EarlyWarningSignalsReadinessState EarlyWarningSignalsReadinessState,
    EarlyWarningSignalsReadinessState SignalCatalogBoundaryState,
    EarlyWarningSignalsReadinessState TalentDataSourceDependencyState,
    EarlyWarningSignalsReadinessState AlertRoutingBoundaryState,
    EarlyWarningSignalsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record EarlyWarningSignalsAuditMetadataDto(
    Guid Id,
    string Code,
    EarlyWarningSignalsReadinessState RetentionPolicyState,
    EarlyWarningSignalsReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class EarlyWarningSignalsMapper
{
    public static EarlyWarningSignalsReadinessDto ToDto(EarlyWarningSignalsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EarlyWarningSignalsReadinessState,
            entity.SignalCatalogBoundaryState,
            entity.PatternDetectionBoundaryState,
            entity.CrossCompanyCorrelationBoundaryState,
            entity.AlertRoutingBoundaryState,
            entity.SignalReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.RiskIndicatorSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.EarlyWarningSignalsReadinessVersion,
            entity.DeferredReason);

    public static EarlyWarningSignalsReadinessListItemDto ToListItem(EarlyWarningSignalsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.EarlyWarningSignalsReadinessState,
            entity.SignalCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.AlertRoutingBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static EarlyWarningSignalsAuditMetadataDto ToAuditMetadata(EarlyWarningSignalsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
