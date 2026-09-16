using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.CandidatePipeline;

// Enum field values map 1:1 to the backend CandidatePipelineReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class CandidatePipelineEditViewModel
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
    public long PipelineReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int PipelineReadinessState { get; set; } = 0;                    // Draft
    public int PipelineStageGovernanceState { get; set; } = 2;              // Deferred
    public int InterviewSchedulingReadinessState { get; set; } = 2;         // Deferred
    public int InterviewerAssignmentReadinessState { get; set; } = 2;       // Deferred
    public int EvaluationGovernanceState { get; set; } = 2;                 // Deferred
    public int CandidateCommunicationBoundaryState { get; set; } = 3;       // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;            // Blocked

    // Dependencies
    public int CalendarDependencyState { get; set; } = 2;                   // Deferred
    public int NotificationDependencyState { get; set; } = 2;               // Deferred
    public int DocumentDependencyState { get; set; } = 2;                   // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int EvidencePolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class CandidatePipelineDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int PipelineReadinessState { get; set; }
    public int PipelineStageGovernanceState { get; set; }
    public int InterviewSchedulingReadinessState { get; set; }
    public int InterviewerAssignmentReadinessState { get; set; }
    public int EvaluationGovernanceState { get; set; }
    public int CandidateCommunicationBoundaryState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public int CalendarDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long PipelineReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring CandidatePipelineReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match CandidatePipelineCreateRequest.
// The DependencyStates dictionary is intentionally NOT sent from the compact form;
// the service applies its own default (empty dictionary).
public sealed class CandidatePipelineSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int PipelineReadinessState { get; set; }
    public int PipelineStageGovernanceState { get; set; }
    public int InterviewSchedulingReadinessState { get; set; }
    public int InterviewerAssignmentReadinessState { get; set; }
    public int EvaluationGovernanceState { get; set; }
    public int CandidateCommunicationBoundaryState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public int CalendarDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long PipelineReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
