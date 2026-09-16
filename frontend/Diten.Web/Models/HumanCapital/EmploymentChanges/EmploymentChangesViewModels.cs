using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.EmploymentChanges;

// Enum field values map 1:1 to the backend EmploymentChangeReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class EmploymentChangesEditViewModel
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
    public long EmploymentChangeReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int EmploymentChangeReadinessState { get; set; } = 0;      // Draft
    public int ChangeLifecycleBoundaryState { get; set; } = 3;        // Blocked
    public int TransferBoundaryState { get; set; } = 3;              // Blocked
    public int PromotionBoundaryState { get; set; } = 3;            // Blocked
    public int ApprovalBoundaryState { get; set; } = 3;            // Blocked
    public int PositionAssignmentBoundaryState { get; set; } = 3;   // Blocked
    public int EmployeeActionBoundaryState { get; set; } = 3;       // Blocked
    public int ManagerActionBoundaryState { get; set; } = 3;        // Blocked
    public int CompensationDataBoundaryState { get; set; } = 3;     // Blocked
    public int BenefitsDataBoundaryState { get; set; } = 3;         // Blocked
    public int PayrollDataBoundaryState { get; set; } = 3;          // Blocked

    // Dependencies
    public int DocumentDependencyState { get; set; } = 2;           // Deferred
    public int NotificationDependencyState { get; set; } = 2;       // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int EvidencePolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class EmploymentChangesDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int EmploymentChangeReadinessState { get; set; }
    public int ChangeLifecycleBoundaryState { get; set; }
    public int TransferBoundaryState { get; set; }
    public int PromotionBoundaryState { get; set; }
    public int ApprovalBoundaryState { get; set; }
    public int PositionAssignmentBoundaryState { get; set; }
    public int EmployeeActionBoundaryState { get; set; }
    public int ManagerActionBoundaryState { get; set; }
    public int CompensationDataBoundaryState { get; set; }
    public int BenefitsDataBoundaryState { get; set; }
    public int PayrollDataBoundaryState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long EmploymentChangeReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring EmploymentChangeReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match EmploymentChangeReadinessCreateRequest.
// This module has no manager/employee UX-reserved fields; ManagerActionBoundaryState /
// EmployeeActionBoundaryState are action boundaries and ARE sent.
public sealed class EmploymentChangesSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int EmploymentChangeReadinessState { get; set; }
    public int ChangeLifecycleBoundaryState { get; set; }
    public int TransferBoundaryState { get; set; }
    public int PromotionBoundaryState { get; set; }
    public int ApprovalBoundaryState { get; set; }
    public int PositionAssignmentBoundaryState { get; set; }
    public int EmployeeActionBoundaryState { get; set; }
    public int ManagerActionBoundaryState { get; set; }
    public int CompensationDataBoundaryState { get; set; }
    public int BenefitsDataBoundaryState { get; set; }
    public int PayrollDataBoundaryState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long EmploymentChangeReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
