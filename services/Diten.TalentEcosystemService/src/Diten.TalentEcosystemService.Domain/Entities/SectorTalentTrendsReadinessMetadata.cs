using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class SectorTalentTrendsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SectorTalentTrendsReadinessState SectorTalentTrendsReadinessState { get; set; }
    public SectorTalentTrendsReadinessState TrendCatalogBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState SignalBindingIntakeBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState AggregationScopeBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState VisibilityControlBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState TrendReviewBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public SectorTalentTrendsReadinessState TalentDataSourceDependencyState { get; set; }
    public SectorTalentTrendsReadinessState WorkforceAnalyticsSourceDependencyState { get; set; }
    public SectorTalentTrendsReadinessState DataGovernancePolicyDependencyState { get; set; }
    public SectorTalentTrendsReadinessState NotificationDependencyState { get; set; }
    public SectorTalentTrendsReadinessState ConsentPreconditionState { get; set; }
    public SectorTalentTrendsReadinessState DataMinimizationState { get; set; }
    public SectorTalentTrendsReadinessState RetentionPolicyState { get; set; }
    public SectorTalentTrendsReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, SectorTalentTrendsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long SectorTalentTrendsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
