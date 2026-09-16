using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.Succession;

// Enum field values map 1:1 to the backend SuccessionReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class SuccessionEditViewModel
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
    public long SuccessionReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int SuccessionReadinessState { get; set; } = 0;                     // Draft
    public int SuccessionPoolBoundaryState { get; set; } = 3;                  // Blocked
    public int HighPotentialIdentificationBoundaryState { get; set; } = 3;     // Blocked
    public int NominationWorkflowBoundaryState { get; set; } = 3;              // Blocked
    public int ReadinessAssessmentBoundaryState { get; set; } = 3;            // Blocked
    public int TalentReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int TalentProfileDependencyState { get; set; } = 2;               // Deferred
    public int PositionFrameworkDependencyState { get; set; } = 2;           // Deferred
    public int DocumentDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int EvidencePolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class SuccessionDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SuccessionReadinessState { get; set; }
    public int SuccessionPoolBoundaryState { get; set; }
    public int HighPotentialIdentificationBoundaryState { get; set; }
    public int NominationWorkflowBoundaryState { get; set; }
    public int ReadinessAssessmentBoundaryState { get; set; }
    public int TalentReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int TalentProfileDependencyState { get; set; }
    public int PositionFrameworkDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long SuccessionReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring SuccessionReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match SuccessionReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class SuccessionSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SuccessionReadinessState { get; set; }
    public int SuccessionPoolBoundaryState { get; set; }
    public int HighPotentialIdentificationBoundaryState { get; set; }
    public int NominationWorkflowBoundaryState { get; set; }
    public int ReadinessAssessmentBoundaryState { get; set; }
    public int TalentReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int TalentProfileDependencyState { get; set; }
    public int PositionFrameworkDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long SuccessionReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
