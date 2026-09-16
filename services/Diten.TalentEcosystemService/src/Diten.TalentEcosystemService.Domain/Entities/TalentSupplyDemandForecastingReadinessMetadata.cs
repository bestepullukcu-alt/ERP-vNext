using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TalentSupplyDemandForecastingReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TalentSupplyDemandForecastingReadinessState TalentSupplyDemandForecastingReadinessState { get; set; }
    public TalentSupplyDemandForecastingReadinessState ForecastCatalogBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState ModelBindingIntakeBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState HorizonScopeBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState VisibilityControlBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState ForecastReviewBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState AutomatedDecisionBoundaryState { get; set; }
    public TalentSupplyDemandForecastingReadinessState TalentDataSourceDependencyState { get; set; }
    public TalentSupplyDemandForecastingReadinessState WorkforceAnalyticsSourceDependencyState { get; set; }
    public TalentSupplyDemandForecastingReadinessState SectorTrendSourceDependencyState { get; set; }
    public TalentSupplyDemandForecastingReadinessState NotificationDependencyState { get; set; }
    public TalentSupplyDemandForecastingReadinessState ConsentPreconditionState { get; set; }
    public TalentSupplyDemandForecastingReadinessState DataMinimizationState { get; set; }
    public TalentSupplyDemandForecastingReadinessState RetentionPolicyState { get; set; }
    public TalentSupplyDemandForecastingReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, TalentSupplyDemandForecastingReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long TalentSupplyDemandForecastingReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
