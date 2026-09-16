using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class IndustryKnowledgeNetworkReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IndustryKnowledgeNetworkReadinessState IndustryKnowledgeNetworkReadinessState { get; set; }
    public IndustryKnowledgeNetworkReadinessState KnowledgeCatalogBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState ContentBindingIntakeBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState NetworkScopeBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState VisibilityControlBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState KnowledgeReviewBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState AutomatedDecisionBoundaryState { get; set; }
    public IndustryKnowledgeNetworkReadinessState KnowledgeSourceRegistryDependencyState { get; set; }
    public IndustryKnowledgeNetworkReadinessState SectorTrendSourceDependencyState { get; set; }
    public IndustryKnowledgeNetworkReadinessState DataGovernancePolicyDependencyState { get; set; }
    public IndustryKnowledgeNetworkReadinessState AssociationOperationsSourceDependencyState { get; set; }
    public IndustryKnowledgeNetworkReadinessState ConsentPreconditionState { get; set; }
    public IndustryKnowledgeNetworkReadinessState DataMinimizationState { get; set; }
    public IndustryKnowledgeNetworkReadinessState RetentionPolicyState { get; set; }
    public IndustryKnowledgeNetworkReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, IndustryKnowledgeNetworkReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long IndustryKnowledgeNetworkReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
