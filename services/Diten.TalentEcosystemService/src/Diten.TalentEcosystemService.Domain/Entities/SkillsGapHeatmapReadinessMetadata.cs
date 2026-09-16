using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class SkillsGapHeatmapReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SkillsGapHeatmapReadinessState SkillsGapHeatmapReadinessState { get; set; }
    public SkillsGapHeatmapReadinessState GapCatalogBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState HeatmapBindingIntakeBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState SeverityScopeBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState VisibilityControlBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState GapReviewBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState AutomatedDecisionBoundaryState { get; set; }
    public SkillsGapHeatmapReadinessState SkillsTaxonomySourceDependencyState { get; set; }
    public SkillsGapHeatmapReadinessState WorkforceAnalyticsSourceDependencyState { get; set; }
    public SkillsGapHeatmapReadinessState TalentDemandForecastSourceDependencyState { get; set; }
    public SkillsGapHeatmapReadinessState NotificationDependencyState { get; set; }
    public SkillsGapHeatmapReadinessState ConsentPreconditionState { get; set; }
    public SkillsGapHeatmapReadinessState DataMinimizationState { get; set; }
    public SkillsGapHeatmapReadinessState RetentionPolicyState { get; set; }
    public SkillsGapHeatmapReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, SkillsGapHeatmapReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long SkillsGapHeatmapReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
