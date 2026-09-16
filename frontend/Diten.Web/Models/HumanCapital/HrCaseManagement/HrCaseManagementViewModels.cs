using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.HrCaseManagement;

// Enum field values map 1:1 to the backend HrCaseManagementReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class HrCaseManagementEditViewModel
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
    public long HrCaseManagementReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int HrCaseManagementReadinessState { get; set; } = 0;                     // Draft
    public int CaseIntakeBoundaryState { get; set; } = 3;                  // Blocked
    public int CaseTriageBoundaryState { get; set; } = 3;     // Blocked
    public int InvestigationTrackingBoundaryState { get; set; } = 3;              // Blocked
    public int DisciplinaryActionBoundaryState { get; set; } = 3;            // Blocked
    public int ResolutionClosureBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int EmployeeRecordDependencyState { get; set; } = 2;               // Deferred
    public int SensitiveAccessPolicyDependencyState { get; set; } = 2;           // Deferred
    public int DocumentDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int EvidencePolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class HrCaseManagementDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int HrCaseManagementReadinessState { get; set; }
    public int CaseIntakeBoundaryState { get; set; }
    public int CaseTriageBoundaryState { get; set; }
    public int InvestigationTrackingBoundaryState { get; set; }
    public int DisciplinaryActionBoundaryState { get; set; }
    public int ResolutionClosureBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int EmployeeRecordDependencyState { get; set; }
    public int SensitiveAccessPolicyDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long HrCaseManagementReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring HrCaseManagementReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match HrCaseManagementReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class HrCaseManagementSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int HrCaseManagementReadinessState { get; set; }
    public int CaseIntakeBoundaryState { get; set; }
    public int CaseTriageBoundaryState { get; set; }
    public int InvestigationTrackingBoundaryState { get; set; }
    public int DisciplinaryActionBoundaryState { get; set; }
    public int ResolutionClosureBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int EmployeeRecordDependencyState { get; set; }
    public int SensitiveAccessPolicyDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long HrCaseManagementReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
