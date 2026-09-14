using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class PayBenchmarkingReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PayBenchmarkingReadinessState PayBenchmarkingReadinessState { get; set; }
    public PayBenchmarkingReadinessState ReferenceRangeCatalogBoundaryState { get; set; }
    public PayBenchmarkingReadinessState ContributionIntakeBoundaryState { get; set; }
    public PayBenchmarkingReadinessState AggregationScopeBoundaryState { get; set; }
    public PayBenchmarkingReadinessState VisibilityControlBoundaryState { get; set; }
    public PayBenchmarkingReadinessState BenchmarkingReviewBoundaryState { get; set; }
    public PayBenchmarkingReadinessState AutomatedDecisionBoundaryState { get; set; }
    public PayBenchmarkingReadinessState TalentDataSourceDependencyState { get; set; }
    public PayBenchmarkingReadinessState ConsentPolicyDependencyState { get; set; }
    public PayBenchmarkingReadinessState DataGovernancePolicyDependencyState { get; set; }
    public PayBenchmarkingReadinessState NotificationDependencyState { get; set; }
    public PayBenchmarkingReadinessState ConsentPreconditionState { get; set; }
    public PayBenchmarkingReadinessState DataMinimizationState { get; set; }
    public PayBenchmarkingReadinessState RetentionPolicyState { get; set; }
    public PayBenchmarkingReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, PayBenchmarkingReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long PayBenchmarkingReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
