using Diten.HumanCapitalService.Domain.Common;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Domain.Entities;

public sealed class HrDocumentationReadinessMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public HrDocumentationReadinessState HrDocumentationReadinessState { get; set; }
    public HrDocumentationReadinessState DocumentWorkspaceBoundaryState { get; set; }
    public HrDocumentationReadinessState EvidenceLinkBoundaryState { get; set; }
    public HrDocumentationReadinessState DocumentClassificationBoundaryState { get; set; }
    public HrDocumentationReadinessState LegalHoldBoundaryState { get; set; }
    public HrDocumentationReadinessState DispositionScheduleBoundaryState { get; set; }
    public HrDocumentationReadinessState AutomatedDecisionBoundaryState { get; set; }
    public HrDocumentationReadinessState DocumentRepositoryDependencyState { get; set; }
    public HrDocumentationReadinessState EvidenceStoreDependencyState { get; set; }
    public HrDocumentationReadinessState DocumentDependencyState { get; set; }
    public HrDocumentationReadinessState NotificationDependencyState { get; set; }
    public HrDocumentationReadinessState ConsentPreconditionState { get; set; }
    public HrDocumentationReadinessState DataMinimizationState { get; set; }
    public HrDocumentationReadinessState RetentionPolicyState { get; set; }
    public HrDocumentationReadinessState EvidencePolicyState { get; set; }
    public Dictionary<string, HrDocumentationReadinessState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public long HrDocumentationReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}
