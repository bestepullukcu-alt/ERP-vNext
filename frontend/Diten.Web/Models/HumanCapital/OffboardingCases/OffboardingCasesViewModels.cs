using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.OffboardingCases;

// Enum field values map 1:1 to the backend ordinals and are serialized as integers by the
// service (no JsonStringEnumConverter), so the save payload carries integers and the gateway
// binds them back to the enums.
//   OffboardingState:                Draft=0, ReviewRequired=1, Approved=2, Active=3,
//                                    TepHandoffReady=4, Completed=5, Cancelled=6, Archived=7 (Archived set by archive action).
//   OffboardingChecklistState:       NotStarted=0, Planned=1, Deferred=2, InProgress=3, Completed=4.
//   OffboardingSensitiveAccessDecisionState (display only): Deferred=0, Allowed=1, Denied=2.
//   OffboardingDependencyDecisionState: Deferred=0, Available=1, Unavailable=2.
//   OffboardingTepHandoffState:      NotRequired=0, Planned=1, Ready=2, Deferred=3, Transferred=4.

public sealed class OffboardingCasesEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    // Required non-empty. Backend validates Guid.Empty and surfaces the gateway error.
    [Required]
    public Guid EmployeeProjectionId { get; set; }

    // Optional; empty text binds to null.
    public Guid? AssignmentOverlayId { get; set; }

    [Required]
    [StringLength(64)]
    public string ExitReasonCode { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string ExitTypeCode { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? NoticeDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime? PlannedExitDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ActualExitDate { get; set; }

    // Draft=0 default. Form offers Draft..Cancelled (0..6). Archived(7) excluded (archive action only).
    public int OffboardingState { get; set; } = 0;

    // NotStarted=0 default.
    public int ChecklistState { get; set; } = 0;

    // Deferred=0 default.
    public int DependencyDecisionState { get; set; } = 0;

    // NotRequired=0 default.
    public int TepHandoffState { get; set; } = 0;

    [StringLength(64)]
    public string? TepHandoffReferenceKey { get; set; }

    [Required]
    [StringLength(32)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long OffboardingVersion { get; set; } = 1;
}

public sealed class OffboardingCasesReviewViewModel
{
    public Guid Id { get; set; }

    // Display-only anchor shown on the focused review page.
    public string Code { get; set; } = string.Empty;

    // ReviewRequired=1 default.
    public int OffboardingState { get; set; } = 1;

    // Deferred=0 default.
    public int DependencyDecisionState { get; set; } = 0;

    [Required]
    [StringLength(32)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long OffboardingVersion { get; set; } = 1;
}

public sealed class OffboardingCasesHandoffViewModel
{
    public Guid Id { get; set; }

    // Display-only anchor shown on the focused handoff page.
    public string Code { get; set; } = string.Empty;

    // Planned=1 default (NotRequired=0, Planned=1, Ready=2, Deferred=3; Transferred=4 rejected by backend).
    public int TepHandoffState { get; set; } = 1;

    [StringLength(64)]
    public string? TepHandoffReferenceKey { get; set; }

    [Required]
    [StringLength(32)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long OffboardingVersion { get; set; } = 1;
}

public sealed class OffboardingCasesDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid? AssignmentOverlayId { get; set; }
    public string ExitReasonCode { get; set; } = string.Empty;
    public string ExitTypeCode { get; set; } = string.Empty;
    public DateTimeOffset? NoticeDate { get; set; }
    public DateTimeOffset PlannedExitDate { get; set; }
    public DateTimeOffset? ActualExitDate { get; set; }
    public int OffboardingState { get; set; }
    public int ChecklistState { get; set; }
    public int SensitiveAccessDecisionState { get; set; }
    public int DependencyDecisionState { get; set; }
    public int TepHandoffState { get; set; }
    public string? TepHandoffReferenceKey { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long OffboardingVersion { get; set; }
    public DateTimeOffset? LastDependencyEvaluatedAt { get; set; }
    public DateTimeOffset? LastHandoffPlannedAt { get; set; }
    public string? DeferredReason { get; set; }
}

// Sent to the gateway create/update endpoints. Property names match OffboardingCaseCreateRequest
// (OffboardingCaseUpdateRequest inherits the same fields).
public sealed class OffboardingCasesSavePayload
{
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid? AssignmentOverlayId { get; set; }
    public string ExitReasonCode { get; set; } = string.Empty;
    public string ExitTypeCode { get; set; } = string.Empty;
    public DateTimeOffset? NoticeDate { get; set; }
    public DateTimeOffset PlannedExitDate { get; set; }
    public DateTimeOffset? ActualExitDate { get; set; }
    public int OffboardingState { get; set; }
    public int ChecklistState { get; set; }
    public int DependencyDecisionState { get; set; }
    public int TepHandoffState { get; set; }
    public string? TepHandoffReferenceKey { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long OffboardingVersion { get; set; } = 1;
}

// Sent to the gateway {id}/review endpoint. Matches OffboardingCaseReviewRequest.
public sealed class OffboardingCasesReviewPayload
{
    public int OffboardingState { get; set; }
    public int DependencyDecisionState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long OffboardingVersion { get; set; } = 1;
}

// Sent to the gateway {id}/handoff endpoint. Matches OffboardingCaseHandoffRequest.
public sealed class OffboardingCasesHandoffPayload
{
    public int TepHandoffState { get; set; }
    public string? TepHandoffReferenceKey { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long OffboardingVersion { get; set; } = 1;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
