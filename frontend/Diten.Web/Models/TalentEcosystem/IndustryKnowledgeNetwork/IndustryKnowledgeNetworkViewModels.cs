using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TalentEcosystem.IndustryKnowledgeNetwork;

// Enum field values map 1:1 to the backend IndustryKnowledgeNetworkReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class IndustryKnowledgeNetworkEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long IndustryKnowledgeNetworkReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int IndustryKnowledgeNetworkReadinessState { get; set; } = 0;                     // Draft
    public int KnowledgeCatalogBoundaryState { get; set; } = 3;                  // Blocked
    public int ContentBindingIntakeBoundaryState { get; set; } = 3;     // Blocked
    public int NetworkScopeBoundaryState { get; set; } = 3;              // Blocked
    public int VisibilityControlBoundaryState { get; set; } = 3;            // Blocked
    public int KnowledgeReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int KnowledgeSourceRegistryDependencyState { get; set; } = 2;               // Deferred
    public int SectorTrendSourceDependencyState { get; set; } = 2;           // Deferred
    public int DataGovernancePolicyDependencyState { get; set; } = 2;                    // Deferred
    public int AssociationOperationsSourceDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int PublicationPolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class IndustryKnowledgeNetworkDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int IndustryKnowledgeNetworkReadinessState { get; set; }
    public int KnowledgeCatalogBoundaryState { get; set; }
    public int ContentBindingIntakeBoundaryState { get; set; }
    public int NetworkScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int KnowledgeReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int KnowledgeSourceRegistryDependencyState { get; set; }
    public int SectorTrendSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int AssociationOperationsSourceDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long IndustryKnowledgeNetworkReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring IndustryKnowledgeNetworkReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match IndustryKnowledgeNetworkReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class IndustryKnowledgeNetworkSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int IndustryKnowledgeNetworkReadinessState { get; set; }
    public int KnowledgeCatalogBoundaryState { get; set; }
    public int ContentBindingIntakeBoundaryState { get; set; }
    public int NetworkScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int KnowledgeReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int KnowledgeSourceRegistryDependencyState { get; set; }
    public int SectorTrendSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int AssociationOperationsSourceDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long IndustryKnowledgeNetworkReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
