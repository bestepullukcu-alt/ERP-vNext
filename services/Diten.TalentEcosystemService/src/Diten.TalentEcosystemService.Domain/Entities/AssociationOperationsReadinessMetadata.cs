using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class AssociationOperationsReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AssociationOperationsReadinessState AssociationOperationsReadinessState { get; set; }
    public AssociationOperationsReadinessState MembershipCatalogBoundaryState { get; set; }
    public AssociationOperationsReadinessState ServiceBindingIntakeBoundaryState { get; set; }
    public AssociationOperationsReadinessState ProgramScopeBoundaryState { get; set; }
    public AssociationOperationsReadinessState VisibilityControlBoundaryState { get; set; }
    public AssociationOperationsReadinessState OperationsReviewBoundaryState { get; set; }
    public AssociationOperationsReadinessState AutomatedDecisionBoundaryState { get; set; }
    public AssociationOperationsReadinessState MemberRegistrySourceDependencyState { get; set; }
    public AssociationOperationsReadinessState SectorTrendSourceDependencyState { get; set; }
    public AssociationOperationsReadinessState DataGovernancePolicyDependencyState { get; set; }
    public AssociationOperationsReadinessState NotificationDependencyState { get; set; }
    public AssociationOperationsReadinessState ConsentPreconditionState { get; set; }
    public AssociationOperationsReadinessState DataMinimizationState { get; set; }
    public AssociationOperationsReadinessState RetentionPolicyState { get; set; }
    public AssociationOperationsReadinessState PublicationPolicyState { get; set; }
    public Dictionary<string, AssociationOperationsReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long AssociationOperationsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
