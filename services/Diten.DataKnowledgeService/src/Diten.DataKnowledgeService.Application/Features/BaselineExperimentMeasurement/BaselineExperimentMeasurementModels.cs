using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement;

public class BaselineExperimentMeasurementReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public BaselineExperimentMeasurementReadinessState BaselineExperimentMeasurementReadinessState { get; init; } = BaselineExperimentMeasurementReadinessState.Draft;
    public BaselineExperimentMeasurementReadinessState BaselineCatalogBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState ExperimentDesignIntakeBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState MeasurementBindingScopeBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState ResultPublicationControlBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState ExperimentReviewBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState AutomatedDecisionBoundaryState { get; init; } = BaselineExperimentMeasurementReadinessState.Blocked;
    public BaselineExperimentMeasurementReadinessState MetricSemanticRegistrySourceDependencyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState ScorecardSourceDependencyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState DataContractRegistryDependencyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState NotificationDependencyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState StewardshipPreconditionState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState DataMinimizationState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState RetentionPolicyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public BaselineExperimentMeasurementReadinessState MeasurementPolicyState { get; init; } = BaselineExperimentMeasurementReadinessState.Deferred;
    public IReadOnlyDictionary<string, BaselineExperimentMeasurementReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, BaselineExperimentMeasurementReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long BaselineExperimentMeasurementReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record BaselineExperimentMeasurementReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    BaselineExperimentMeasurementReadinessState BaselineExperimentMeasurementReadinessState,
    BaselineExperimentMeasurementReadinessState BaselineCatalogBoundaryState,
    BaselineExperimentMeasurementReadinessState ExperimentDesignIntakeBoundaryState,
    BaselineExperimentMeasurementReadinessState MeasurementBindingScopeBoundaryState,
    BaselineExperimentMeasurementReadinessState ResultPublicationControlBoundaryState,
    BaselineExperimentMeasurementReadinessState ExperimentReviewBoundaryState,
    BaselineExperimentMeasurementReadinessState AutomatedDecisionBoundaryState,
    BaselineExperimentMeasurementReadinessState MetricSemanticRegistrySourceDependencyState,
    BaselineExperimentMeasurementReadinessState ScorecardSourceDependencyState,
    BaselineExperimentMeasurementReadinessState DataContractRegistryDependencyState,
    BaselineExperimentMeasurementReadinessState NotificationDependencyState,
    BaselineExperimentMeasurementReadinessState StewardshipPreconditionState,
    BaselineExperimentMeasurementReadinessState DataMinimizationState,
    BaselineExperimentMeasurementReadinessState RetentionPolicyState,
    BaselineExperimentMeasurementReadinessState MeasurementPolicyState,
    IReadOnlyDictionary<string, BaselineExperimentMeasurementReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long BaselineExperimentMeasurementReadinessVersion,
    string? DeferredReason);

public sealed record BaselineExperimentMeasurementReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    BaselineExperimentMeasurementReadinessState BaselineExperimentMeasurementReadinessState,
    BaselineExperimentMeasurementReadinessState BaselineCatalogBoundaryState,
    BaselineExperimentMeasurementReadinessState MetricSemanticRegistrySourceDependencyState,
    BaselineExperimentMeasurementReadinessState ResultPublicationControlBoundaryState,
    BaselineExperimentMeasurementReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record BaselineExperimentMeasurementAuditMetadataDto(
    Guid Id,
    string Code,
    BaselineExperimentMeasurementReadinessState RetentionPolicyState,
    BaselineExperimentMeasurementReadinessState MeasurementPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class BaselineExperimentMeasurementMapper
{
    public static BaselineExperimentMeasurementReadinessDto ToDto(BaselineExperimentMeasurementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.BaselineExperimentMeasurementReadinessState,
            entity.BaselineCatalogBoundaryState,
            entity.ExperimentDesignIntakeBoundaryState,
            entity.MeasurementBindingScopeBoundaryState,
            entity.ResultPublicationControlBoundaryState,
            entity.ExperimentReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.ScorecardSourceDependencyState,
            entity.DataContractRegistryDependencyState,
            entity.NotificationDependencyState,
            entity.StewardshipPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.MeasurementPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.BaselineExperimentMeasurementReadinessVersion,
            entity.DeferredReason);

    public static BaselineExperimentMeasurementReadinessListItemDto ToListItem(BaselineExperimentMeasurementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.BaselineExperimentMeasurementReadinessState,
            entity.BaselineCatalogBoundaryState,
            entity.MetricSemanticRegistrySourceDependencyState,
            entity.ResultPublicationControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static BaselineExperimentMeasurementAuditMetadataDto ToAuditMetadata(BaselineExperimentMeasurementReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.MeasurementPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
