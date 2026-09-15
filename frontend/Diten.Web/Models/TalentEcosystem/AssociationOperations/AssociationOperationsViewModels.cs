using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TalentEcosystem.AssociationOperations;

// Enum field values map 1:1 to the backend AssociationOperationsReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class AssociationOperationsEditViewModel
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
    public long AssociationOperationsReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int AssociationOperationsReadinessState { get; set; } = 0;                     // Draft
    public int MembershipCatalogBoundaryState { get; set; } = 3;                  // Blocked
    public int ServiceBindingIntakeBoundaryState { get; set; } = 3;     // Blocked
    public int ProgramScopeBoundaryState { get; set; } = 3;              // Blocked
    public int VisibilityControlBoundaryState { get; set; } = 3;            // Blocked
    public int OperationsReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int MemberRegistrySourceDependencyState { get; set; } = 2;               // Deferred
    public int SectorTrendSourceDependencyState { get; set; } = 2;           // Deferred
    public int DataGovernancePolicyDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int PublicationPolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class AssociationOperationsDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int AssociationOperationsReadinessState { get; set; }
    public int MembershipCatalogBoundaryState { get; set; }
    public int ServiceBindingIntakeBoundaryState { get; set; }
    public int ProgramScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int OperationsReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MemberRegistrySourceDependencyState { get; set; }
    public int SectorTrendSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long AssociationOperationsReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring AssociationOperationsReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match AssociationOperationsReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class AssociationOperationsSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int AssociationOperationsReadinessState { get; set; }
    public int MembershipCatalogBoundaryState { get; set; }
    public int ServiceBindingIntakeBoundaryState { get; set; }
    public int ProgramScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int OperationsReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MemberRegistrySourceDependencyState { get; set; }
    public int SectorTrendSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long AssociationOperationsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
