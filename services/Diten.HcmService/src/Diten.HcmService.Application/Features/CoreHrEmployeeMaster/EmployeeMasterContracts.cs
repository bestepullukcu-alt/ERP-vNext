using System.Text.Json.Serialization;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public sealed record EmployeeRegistryRowResponse(
    Guid EmployeeId,
    string EmployeeNumber,
    Guid PersonId,
    string DisplayName,
    string WorkerType,
    string EmploymentType,
    Guid LegalEntityId,
    string LegalEntityDisplayName,
    string EmployeeStatus,
    string SensitivityLevel,
    DateOnly? HireDate,
    DateTimeOffset UpdatedAt,
    int Version,
    [property: JsonPropertyName("etag")] string ETag,
    EmployeeRowActions Actions);

public sealed record EmployeeRegistrySearchResponse(
    IReadOnlyList<EmployeeRegistryRowResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record EmployeeRowActions(
    bool CanView,
    bool CanEditLegal,
    bool CanEditEmployment,
    bool CanChangeStatus,
    bool CanAttachEvidence,
    bool CanExport);

public sealed record EmployeeDetailResponse(
    Guid EmployeeId,
    string EmployeeNumber,
    Guid PersonId,
    EmployeeLegalProfileResponse LegalProfile,
    IReadOnlyList<EmploymentRecordResponse> EmploymentRecords,
    string EmployeeStatus,
    string SensitivityLevel,
    bool SensitiveFieldsMasked,
    int Version,
    [property: JsonPropertyName("etag")] string ETag,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeLegalProfileResponse(
    string LegalFirstName,
    string? LegalMiddleName,
    string LegalLastName,
    string? PreferredName,
    DateOnly? DateOfBirth,
    string? NationalityCode,
    string? WorkEmail,
    string? PersonalEmail,
    string? Phone,
    bool GovernmentIdentifierPresent);

public sealed record EmploymentRecordResponse(
    Guid EmploymentRecordId,
    Guid LegalEntityId,
    Guid OrganizationUnitId,
    Guid PositionId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string ContractType,
    string? ProbationStatus,
    DateOnly? ProbationEndDate,
    string EmploymentStatus,
    string ApprovalStatus,
    int Version,
    [property: JsonPropertyName("etag")] string ETag);

public sealed record EmployeeProfilePatchRequest(
    [property: JsonPropertyName("etag")] string ETag,
    string? LegalFirstName,
    string? LegalMiddleName,
    string? LegalLastName,
    string? PreferredName,
    DateOnly? DateOfBirth,
    string? NationalityCode,
    string? WorkEmail,
    string? PersonalEmail,
    string? Phone,
    string? SensitivityLevel,
    string IdempotencyKey);

public sealed record EmploymentRecordPatchRequest(
    [property: JsonPropertyName("etag")] string ETag,
    Guid? LegalEntityId,
    Guid? OrganizationUnitId,
    Guid? PositionId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ContractType,
    string? ProbationStatus,
    DateOnly? ProbationEndDate,
    string? EmploymentStatus,
    string IdempotencyKey);

public sealed record EmployeeStatusCommandRequest(
    [property: JsonPropertyName("etag")] string ETag,
    string NewStatus,
    DateOnly EffectiveDate,
    string? ReasonCategory,
    string? ReasonNote,
    Guid? WorkflowReferenceId,
    Guid? EvidenceReferenceId,
    string IdempotencyKey);

public sealed record EmployeeDocumentLinkRequest(
    [property: JsonPropertyName("etag")] string ETag,
    Guid EvidenceId,
    string DocumentType,
    string VisibilityLevel,
    Guid RetentionPolicyId,
    string IdempotencyKey);

public sealed record DataQualityCasePatchRequest(
    [property: JsonPropertyName("etag")] string ETag,
    Guid? AssignedTo,
    string? Status,
    string? ResolutionNote,
    string IdempotencyKey);
