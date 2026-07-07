using System.Text.Json;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public sealed record EmployeeDraftCreateRequest(
    string? SourceContext,
    string? ClientReference,
    string IdempotencyKey);

public sealed record EmployeeDraftCreateResponse(
    Guid DraftSessionId,
    string DraftSchemaVersion,
    string CurrentStep,
    IReadOnlyDictionary<string, string> StepStatuses,
    ReferenceValidationResponse ValidationSummary,
    int Version,
    string ETag,
    DateTimeOffset CreatedAt);

public sealed record EmployeeDraftPatchRequest(
    string StepCode,
    string PayloadSchemaVersion,
    Dictionary<string, JsonElement> StepPayload,
    Dictionary<string, JsonElement>? ClientValidationState,
    string IdempotencyKey);

public sealed record EmployeeDraftResponse(
    Guid DraftSessionId,
    string DraftSchemaVersion,
    string CurrentStep,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps,
    IReadOnlyDictionary<string, string> StepStatuses,
    ReferenceValidationResponse ReferenceValidationSummary,
    string ReviewState,
    int Version,
    string ETag,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ReferenceValidationRequest(
    string? PersonId,
    string? OrganizationUnitId,
    string? PositionId,
    string? LegalEntityId,
    string IdempotencyKey);

public sealed record ReferenceValidationResponse(
    IReadOnlyList<ReferenceValidationItem> Results,
    bool CanReview);

public sealed record ReferenceValidationItem(
    string ReferenceType,
    string ReferenceId,
    string Status,
    bool IsReferenceable,
    string Provider,
    string? ReasonCode,
    IReadOnlyDictionary<string, string> SafeDisplayMetadata);

public sealed record DraftReviewRequest(
    string IdempotencyKey,
    bool ReferenceValidationAcknowledged,
    bool DuplicateWarningAcknowledged,
    string? ETag);

public sealed record DraftReviewResponse(
    Guid DraftSessionId,
    string ReviewState,
    bool CanSubmitLater,
    IReadOnlyList<string> BlockingReasons,
    ReferenceValidationResponse ReferenceValidationSummary,
    int Version,
    string ETag);

public sealed record DraftSubmitRequest(
    string IdempotencyKey,
    string? ETag);

public sealed record DraftSubmitResponse(
    Guid DraftSessionId,
    string WorkflowStatus,
    Guid? WorkflowInstanceId,
    string WorkflowDefinitionKey,
    int? WorkflowDefinitionVersion,
    string WorkflowBusinessKey,
    bool WorkflowStartDeferred,
    IReadOnlyList<string> BlockingReasons,
    int Version,
    string ETag);

public sealed record EmployeeSmokeFixtureResponse(
    Guid EmployeeId,
    Guid TenantId,
    string EmployeeNumber,
    string EmployeeStatus,
    bool Created,
    bool Reused,
    string ApiPath,
    DateTimeOffset UpdatedAt);

public sealed record EmployeeSmokeFixtureCleanupResponse(
    Guid? EmployeeId,
    Guid TenantId,
    string EmployeeNumber,
    bool Deleted,
    bool WasPresent);
