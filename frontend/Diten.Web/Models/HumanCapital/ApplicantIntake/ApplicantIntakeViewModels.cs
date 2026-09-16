using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.ApplicantIntake;

// Enum field values map 1:1 to the backend ApplicantIntakeReadinessState ordinals:
// Draft=0, Deferred=1, Ready=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.
// PublicUxBoundaryState is PUBLIC UX (not manager/employee UX) and is included in the payload.
// The DependencyStates dictionary is intentionally not surfaced in this compact CRUD (service defaults it).

public sealed class ApplicantIntakeEditViewModel
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
    public long ApplicantIntakeVersion { get; set; } = 1;

    // Readiness & workflow
    public int IntakeState { get; set; } = 0;                    // Draft
    public int SourceChannelState { get; set; } = 1;             // Deferred
    public int ApplicantIdentityBoundaryState { get; set; } = 1; // Deferred
    public int PublicUxBoundaryState { get; set; } = 3;          // Blocked

    // Dependencies
    public int DuplicateHandlingState { get; set; } = 1;         // Deferred
    public int DocumentDependencyState { get; set; } = 1;        // Deferred
    public int NotificationDependencyState { get; set; } = 1;    // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 1;       // Deferred
    public int DataMinimizationState { get; set; } = 1;          // Deferred
    public int RetentionPolicyState { get; set; } = 1;           // Deferred
    public int EvidencePolicyState { get; set; } = 1;            // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class ApplicantIntakeDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int IntakeState { get; set; }
    public int SourceChannelState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int DuplicateHandlingState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public int ApplicantIdentityBoundaryState { get; set; }
    public int PublicUxBoundaryState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long ApplicantIntakeVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal -> canonical state name, mirroring ApplicantIntakeReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Deferred", "Ready", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match ApplicantIntakeCreateRequest.
// No manager/employee UX fields exist on this contract; PublicUxBoundaryState is a public-UX
// field and IS sent. The service applies its own defaults for the DependencyStates dictionary.
public sealed class ApplicantIntakeSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int IntakeState { get; set; }
    public int SourceChannelState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int DuplicateHandlingState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int EvidencePolicyState { get; set; }
    public int ApplicantIdentityBoundaryState { get; set; }
    public int PublicUxBoundaryState { get; set; }
    public int DocumentDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long ApplicantIntakeVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
