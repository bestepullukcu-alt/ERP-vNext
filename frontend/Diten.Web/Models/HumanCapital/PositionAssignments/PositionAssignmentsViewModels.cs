using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.PositionAssignments;

// Enum field values map 1:1 to the backend ordinals and are serialized as integers by the
// service (no JsonStringEnumConverter), so the save payload carries integers and the gateway
// binds them back to the enums.
//   AssignmentOverlayState:              Deferred=0, Validated=1, Active=2, Archived=3 (Archived is set by the archive action).
//   AssignmentReferenceValidationState:  Deferred=0, Validated=1, FailedClosed=2 (read-only display badge).
//   AssignmentSensitiveAccessDecisionState: Deferred=0, Allowed=1, Denied=2 (read-only display badge).

public sealed class PositionAssignmentsEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    // Required non-empty per contract. Backend guard rejects Guid.Empty.
    [Required]
    public Guid EmployeeProjectionId { get; set; }

    [Required]
    public Guid PersonReferenceId { get; set; }

    // Optional references: empty text box binds to null (never Guid.Empty) and is sent as null.
    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public Guid? ManagerEmployeeProjectionId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime? EffectiveFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? EffectiveTo { get; set; }

    // Deferred=0 default. Form offers Deferred/Validated/Active only (Archived excluded).
    public int AssignmentState { get; set; } = 0;

    [Required]
    [StringLength(64)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long AssignmentVersion { get; set; } = 1;
}

public sealed class PositionAssignmentsDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ManagerEmployeeProjectionId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int AssignmentState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public int ReferenceValidationState { get; set; }
    public int SensitiveAccessDecisionState { get; set; }
    public long AssignmentVersion { get; set; }
    public DateTimeOffset? LastReferenceValidatedAt { get; set; }
    public string? DeferredReason { get; set; }
}

// Sent to the gateway create/update endpoints. Property names match
// PositionAssignmentCreateRequest (UpdateRequest inherits the same fields).
public sealed class PositionAssignmentsSavePayload
{
    public string Code { get; set; } = string.Empty;
    public Guid EmployeeProjectionId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ManagerEmployeeProjectionId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int AssignmentState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long AssignmentVersion { get; set; } = 1;
}

// Sent to the gateway reference-link endpoint. Property names match
// PositionAssignmentReferenceLinkRequest.
public sealed class PositionAssignmentsReferenceLinkPayload
{
    public Guid PersonReferenceId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
