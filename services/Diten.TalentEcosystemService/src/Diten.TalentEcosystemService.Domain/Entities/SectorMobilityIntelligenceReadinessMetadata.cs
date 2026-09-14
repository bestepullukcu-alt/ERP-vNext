using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class SectorMobilityIntelligenceReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SectorMobilityIntelligenceReadinessState SectorMobilityIntelligenceReadinessState { get; set; }
    public SectorMobilityIntelligenceReadinessState MobilityCatalogBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState FlowBindingIntakeBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState CorridorScopeBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState VisibilityControlBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState MobilityReviewBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState AutomatedDecisionBoundaryState { get; set; }
    public SectorMobilityIntelligenceReadinessState SectorTrendSourceDependencyState { get; set; }
    public SectorMobilityIntelligenceReadinessState WorkforceAnalyticsSourceDependencyState { get; set; }
    public SectorMobilityIntelligenceReadinessState SkillsTaxonomySourceDependencyState { get; set; }
    public SectorMobilityIntelligenceReadinessState NotificationDependencyState { get; set; }
    public SectorMobilityIntelligenceReadinessState ConsentPreconditionState { get; set; }
    public SectorMobilityIntelligenceReadinessState DataMinimizationState { get; set; }
    public SectorMobilityIntelligenceReadinessState RetentionPolicyState { get; set; }
    public SectorMobilityIntelligenceReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, SectorMobilityIntelligenceReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long SectorMobilityIntelligenceReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
