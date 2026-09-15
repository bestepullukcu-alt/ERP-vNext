using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics;

public class HrKpiAnalyticsReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public HrKpiAnalyticsReadinessState HrKpiAnalyticsReadinessState { get; init; } = HrKpiAnalyticsReadinessState.Draft;
    public HrKpiAnalyticsReadinessState KpiCatalogBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState MetricDefinitionBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState DashboardBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState AnalyticsQueryBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState DataExportBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState AutomatedDecisionBoundaryState { get; init; } = HrKpiAnalyticsReadinessState.Blocked;
    public HrKpiAnalyticsReadinessState AnalyticsPlatformDependencyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState DataSourceDependencyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState DocumentDependencyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState NotificationDependencyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState ConsentPreconditionState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState DataMinimizationState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState RetentionPolicyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public HrKpiAnalyticsReadinessState EvidencePolicyState { get; init; } = HrKpiAnalyticsReadinessState.Deferred;
    public IReadOnlyDictionary<string, HrKpiAnalyticsReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, HrKpiAnalyticsReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long HrKpiAnalyticsReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record HrKpiAnalyticsReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrKpiAnalyticsReadinessState HrKpiAnalyticsReadinessState,
    HrKpiAnalyticsReadinessState KpiCatalogBoundaryState,
    HrKpiAnalyticsReadinessState MetricDefinitionBoundaryState,
    HrKpiAnalyticsReadinessState DashboardBoundaryState,
    HrKpiAnalyticsReadinessState AnalyticsQueryBoundaryState,
    HrKpiAnalyticsReadinessState DataExportBoundaryState,
    HrKpiAnalyticsReadinessState AutomatedDecisionBoundaryState,
    HrKpiAnalyticsReadinessState AnalyticsPlatformDependencyState,
    HrKpiAnalyticsReadinessState DataSourceDependencyState,
    HrKpiAnalyticsReadinessState DocumentDependencyState,
    HrKpiAnalyticsReadinessState NotificationDependencyState,
    HrKpiAnalyticsReadinessState ConsentPreconditionState,
    HrKpiAnalyticsReadinessState DataMinimizationState,
    HrKpiAnalyticsReadinessState RetentionPolicyState,
    HrKpiAnalyticsReadinessState EvidencePolicyState,
    IReadOnlyDictionary<string, HrKpiAnalyticsReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long HrKpiAnalyticsReadinessVersion,
    string? DeferredReason);

public sealed record HrKpiAnalyticsReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    HrKpiAnalyticsReadinessState HrKpiAnalyticsReadinessState,
    HrKpiAnalyticsReadinessState KpiCatalogBoundaryState,
    HrKpiAnalyticsReadinessState AnalyticsPlatformDependencyState,
    HrKpiAnalyticsReadinessState AnalyticsQueryBoundaryState,
    HrKpiAnalyticsReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record HrKpiAnalyticsAuditMetadataDto(
    Guid Id,
    string Code,
    HrKpiAnalyticsReadinessState RetentionPolicyState,
    HrKpiAnalyticsReadinessState EvidencePolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class HrKpiAnalyticsMapper
{
    public static HrKpiAnalyticsReadinessDto ToDto(HrKpiAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrKpiAnalyticsReadinessState,
            entity.KpiCatalogBoundaryState,
            entity.MetricDefinitionBoundaryState,
            entity.DashboardBoundaryState,
            entity.AnalyticsQueryBoundaryState,
            entity.DataExportBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.AnalyticsPlatformDependencyState,
            entity.DataSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.HrKpiAnalyticsReadinessVersion,
            entity.DeferredReason);

    public static HrKpiAnalyticsReadinessListItemDto ToListItem(HrKpiAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.HrKpiAnalyticsReadinessState,
            entity.KpiCatalogBoundaryState,
            entity.AnalyticsPlatformDependencyState,
            entity.AnalyticsQueryBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static HrKpiAnalyticsAuditMetadataDto ToAuditMetadata(HrKpiAnalyticsReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
