using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.DataKnowledge.MetricDefinitionsOwnership;

// Enum field values map 1:1 to the backend MetricDefinitionsOwnershipReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class MetricDefinitionsOwnershipEditViewModel
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
    public long MetricDefinitionsOwnershipReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int MetricDefinitionsOwnershipReadinessState { get; set; } = 0;                     // Draft
    public int DefinitionCatalogBoundaryState { get; set; } = 3;                  // Blocked
    public int OwnershipAssignmentIntakeBoundaryState { get; set; } = 3;     // Blocked
    public int StewardshipScopeBoundaryState { get; set; } = 3;              // Blocked
    public int ApprovalControlBoundaryState { get; set; } = 3;            // Blocked
    public int DefinitionReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int MetricSemanticRegistrySourceDependencyState { get; set; } = 2;               // Deferred
    public int KpiCatalogSourceDependencyState { get; set; } = 2;           // Deferred
    public int DataContractRegistryDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int StewardshipPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int ApprovalPolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class MetricDefinitionsOwnershipDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MetricDefinitionsOwnershipReadinessState { get; set; }
    public int DefinitionCatalogBoundaryState { get; set; }
    public int OwnershipAssignmentIntakeBoundaryState { get; set; }
    public int StewardshipScopeBoundaryState { get; set; }
    public int ApprovalControlBoundaryState { get; set; }
    public int DefinitionReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MetricSemanticRegistrySourceDependencyState { get; set; }
    public int KpiCatalogSourceDependencyState { get; set; }
    public int DataContractRegistryDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int StewardshipPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int ApprovalPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long MetricDefinitionsOwnershipReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring MetricDefinitionsOwnershipReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match MetricDefinitionsOwnershipReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class MetricDefinitionsOwnershipSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MetricDefinitionsOwnershipReadinessState { get; set; }
    public int DefinitionCatalogBoundaryState { get; set; }
    public int OwnershipAssignmentIntakeBoundaryState { get; set; }
    public int StewardshipScopeBoundaryState { get; set; }
    public int ApprovalControlBoundaryState { get; set; }
    public int DefinitionReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MetricSemanticRegistrySourceDependencyState { get; set; }
    public int KpiCatalogSourceDependencyState { get; set; }
    public int DataContractRegistryDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int StewardshipPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int ApprovalPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long MetricDefinitionsOwnershipReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
