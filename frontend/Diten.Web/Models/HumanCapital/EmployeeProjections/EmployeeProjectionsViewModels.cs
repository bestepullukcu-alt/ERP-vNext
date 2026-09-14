using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.HumanCapital.EmployeeProjections;

// Enum field values map 1:1 to the backend ordinals and are serialized as integers by the
// service (no JsonStringEnumConverter), so the save payload carries integers and the gateway
// binds them back to the enums.
//   EmployeeProjectionState:        Deferred=0, SourceLinked=1, Validated=2, Archived=3 (Archived is set by the archive action).
//   EmployeeVisibilityClassification: StandardHr=0, SensitiveHr=1, RestrictedHr=2.

public sealed class EmployeeProjectionsEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public Guid HrisSourceProfileId { get; set; }

    [Required]
    public Guid PersonReferenceId { get; set; }

    [Required]
    [StringLength(160)]
    public string ExternalEmployeeReference { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string EmploymentRecordReferenceKey { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string EmploymentStatusCode { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string WorkerTypeCode { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string SourceContractVersion { get; set; } = string.Empty;

    // Deferred=0 default. Form offers Deferred/SourceLinked/Validated only (Archived excluded).
    public int ProjectionState { get; set; } = 0;

    // Required: user must pick StandardHr(0)/SensitiveHr(1)/RestrictedHr(2). Nullable so an
    // unselected value fails validation instead of silently defaulting to StandardHr.
    [Required]
    public int? VisibilityClassification { get; set; }

    [DataType(DataType.Date)]
    public DateTime? SourceLastSyncedAt { get; set; }

    [Range(1, long.MaxValue)]
    public long ProjectionVersion { get; set; } = 1;
}

public sealed class EmployeeProjectionsDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid HrisSourceProfileId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public string ExternalEmployeeReference { get; set; } = string.Empty;
    public string EmploymentRecordReferenceKey { get; set; } = string.Empty;
    public string EmploymentStatusCode { get; set; } = string.Empty;
    public string WorkerTypeCode { get; set; } = string.Empty;
    public string SourceContractVersion { get; set; } = string.Empty;
    public int ProjectionState { get; set; }
    public int VisibilityClassification { get; set; }
    public DateTimeOffset? SourceLastSyncedAt { get; set; }
    public long ProjectionVersion { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? DeferredReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

// Sent to the gateway create/update/source-link endpoints. Property names match
// EmployeeProjectionCreateRequest (UpdateRequest inherits the same fields).
public sealed class EmployeeProjectionsSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid HrisSourceProfileId { get; set; }
    public Guid PersonReferenceId { get; set; }
    public string ExternalEmployeeReference { get; set; } = string.Empty;
    public string EmploymentRecordReferenceKey { get; set; } = string.Empty;
    public string EmploymentStatusCode { get; set; } = string.Empty;
    public string WorkerTypeCode { get; set; } = string.Empty;
    public string SourceContractVersion { get; set; } = string.Empty;
    public int ProjectionState { get; set; }
    public int VisibilityClassification { get; set; }
    public DateTimeOffset? SourceLastSyncedAt { get; set; }
    public long ProjectionVersion { get; set; } = 1;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
