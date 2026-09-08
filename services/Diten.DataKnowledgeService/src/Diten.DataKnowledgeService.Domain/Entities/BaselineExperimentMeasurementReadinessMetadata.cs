using Diten.DataKnowledgeService.Domain.Common;
using Diten.DataKnowledgeService.Domain.Enums;

namespace Diten.DataKnowledgeService.Domain.Entities;

public sealed class BaselineExperimentMeasurementReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public BaselineExperimentMeasurementReadinessState BaselineExperimentMeasurementReadinessState { get; set; }
    public BaselineExperimentMeasurementReadinessState BaselineCatalogBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState ExperimentDesignIntakeBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState MeasurementBindingScopeBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState ResultPublicationControlBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState ExperimentReviewBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState AutomatedDecisionBoundaryState { get; set; }
    public BaselineExperimentMeasurementReadinessState MetricSemanticRegistrySourceDependencyState { get; set; }
    public BaselineExperimentMeasurementReadinessState ScorecardSourceDependencyState { get; set; }
    public BaselineExperimentMeasurementReadinessState DataContractRegistryDependencyState { get; set; }
    public BaselineExperimentMeasurementReadinessState NotificationDependencyState { get; set; }
    public BaselineExperimentMeasurementReadinessState StewardshipPreconditionState { get; set; }
    public BaselineExperimentMeasurementReadinessState DataMinimizationState { get; set; }
    public BaselineExperimentMeasurementReadinessState RetentionPolicyState { get; set; }
    public BaselineExperimentMeasurementReadinessState MeasurementPolicyState { get; set; }
    public Dictionary<string, BaselineExperimentMeasurementReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long BaselineExperimentMeasurementReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
