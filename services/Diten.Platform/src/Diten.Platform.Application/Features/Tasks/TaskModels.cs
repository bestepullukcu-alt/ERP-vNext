using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks;

// MOD-0024 — ALL task DTOs live in this single models file (live Platform convention). Permission CONSTANTS are
// declared here only; the seed/grant belongs to MOD-0018 / Diten.AuthService and is a separate task. The keys are
// attributed to Module="tasks" + Scope=Tenant through the MODULE MANIFEST (see TaskManifestProvider): a key first
// created by the A1 reflection worker would be stamped Module="platform" + Scope=PlatformAdmin, which AuthService
// cannot downgrade. The startup ordering gate makes the manifest win.

public static class TaskPermissions
{
    public const string Read = "platform.tasks.read";
    public const string Create = "platform.tasks.create";
    public const string Update = "platform.tasks.update";
    public const string Delete = "platform.tasks.delete";
    public const string BulkDelete = "platform.tasks.bulk-delete";
    public const string Assign = "platform.tasks.assign";
    public const string Claim = "platform.tasks.claim";
    public const string Complete = "platform.tasks.complete";
    public const string Cancel = "platform.tasks.cancel";
    public const string FieldDefinitionsManage = "platform.tasks.field-definitions.manage";
}

public static class TaskReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "TASK_NOT_FOUND";
    public const string AssignmentTargetInvalid = "ASSIGNMENT_TARGET_INVALID";
    public const string PositionNotAssignable = "POSITION_NOT_ASSIGNABLE";
    public const string AssigneeInvalid = "ASSIGNEE_INVALID";
    public const string OrganizationUnitUnresolved = "ORGANIZATION_UNIT_UNRESOLVED";
    public const string AlreadyClaimed = "TASK_ALREADY_CLAIMED";
    public const string NotClaimable = "TASK_NOT_CLAIMABLE";
    public const string InvalidState = "TASK_INVALID_STATE";
    public const string ConcurrencyConflict = "TASK_CONCURRENCY_CONFLICT";
    public const string SpentHoursNotSettable = "SPENT_HOURS_NOT_SETTABLE";
    public const string FieldDefinitionUnknown = "TASK_FIELD_DEFINITION_UNKNOWN";
    public const string FieldValueInvalid = "TASK_FIELD_VALUE_INVALID";
    public const string FieldLimitExceeded = "TASK_FIELD_LIMIT_EXCEEDED";
    public const string ChecklistIncomplete = "CHECKLIST_INCOMPLETE";
    public const string DependencyInvalid = "TASK_DEPENDENCY_INVALID";
}

/// <summary>
/// Notification event codes this module declares in its manifest (pack §14). Email only — there is no in-app
/// channel (<c>NotificationChannelCode { Email = 0 }</c>); the header bell is BL-025.
/// </summary>
public static class TaskNotificationEvents
{
    // NOTE: event codes are validated against ^[a-z0-9]+(\.[a-z0-9]+)*$ — HYPHENS ARE NOT ALLOWED (unlike
    // permission keys, which do permit kebab-case). A hyphenated code fails validation, which forces the event to
    // stay Draft, and a Draft event never dispatches — the email would silently never arrive.
    public const string Assigned = "platform.tasks.assigned";
    public const string Claimed = "platform.tasks.claimed";
    public const string DueSoon = "platform.tasks.duesoon";
    public const string Completed = "platform.tasks.completed";
    public const string ApprovalRequested = "platform.tasks.approvalrequested";
}

/// <summary>Contract limits, mirrored from fixture-contract.js LIMITS. The contract is the authority.</summary>
public static class TaskFieldLimits
{
    public const int MaxSections = 6;
    public const int MaxFieldsPerSection = 8;
    public const int MaxTextLengthPerField = 2000;
    public const int MaxPrimaryFields = 8;
    public const int MaxRelatedRecords = 20;
    public const int MaxTags = 20;
    public const int MaxTagLength = 40;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
}

// ── Requests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Create payload. TenantId is intentionally absent (never client-supplied). <c>Lifecycle</c> is absent too: the
/// system decides the initial state (pack §12 Y2). <c>SpentHours</c> is absent by design (pack §12 Y1).
/// </summary>
public sealed record CreateTaskItemRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    TaskAssignmentTarget AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    IReadOnlyList<string>? Tags,
    bool ReviewRequired,
    bool ApprovalRequired,
    Guid? ApprovalManagerUserId,
    bool EmailNotificationsEnabled,
    bool DelegationAllowed,
    IReadOnlyList<TaskFieldValueDto>? FieldValues,
    IReadOnlyList<TaskWatcherRequest>? Watchers);

public sealed record UpdateTaskItemRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    IReadOnlyList<string>? Tags,
    bool ReviewRequired,
    bool EmailNotificationsEnabled,
    bool DelegationAllowed,
    IReadOnlyList<TaskFieldValueDto>? FieldValues,
    int ExpectedVersion);

public sealed record TaskWatcherRequest(Guid UserId, TaskWatcherRole Role, Guid? PositionId);

public sealed record TaskFieldValueDto(string DefinitionCode, TaskFieldValueType ValueType, string? Value);

public sealed record BulkDeleteTaskItemRequest(IReadOnlyList<Guid> Ids);

public sealed record ClaimTaskItemRequest(int ExpectedVersion);

public sealed record TaskTransitionRequest(int ExpectedVersion, string? ReasonCode, string? Note);

// ── Responses ────────────────────────────────────────────────────────────────

public sealed record TaskItemListItemDto(
    Guid Id,
    string Title,
    string Lifecycle,
    string NormalizedStatus,
    string Priority,
    string AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid OrganizationUnitId,
    DateTimeOffset? DueAt,
    bool ReviewRequired,
    bool ApprovalRequired,
    int Version,
    DateTimeOffset CreatedAt);

public sealed record TaskItemDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string Lifecycle,
    string NormalizedStatus,
    string Priority,
    string AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? CreatedByUserId,
    Guid OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    decimal SpentHours,
    decimal? RemainingHours,
    IReadOnlyList<string> Tags,
    bool ReviewRequired,
    bool ApprovalRequired,
    Guid? ApprovalManagerUserId,
    Guid? WorkflowInstanceId,
    bool EmailNotificationsEnabled,
    bool DelegationAllowed,
    string? ProcessInstanceId,
    IReadOnlyList<TaskFieldValueDto> FieldValues,
    IReadOnlyList<TaskWatcherDto> Watchers,
    IReadOnlyList<TaskDependencyDto> Dependencies,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? ClosureReasonCode,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TaskWatcherDto(Guid Id, Guid UserId, string Role, Guid? PositionId);

public sealed record TaskDependencyDto(Guid Id, Guid DependsOnTaskItemId, string DependencyType);

/// <summary>
/// Assignable-position lookup row (pack §12 K4). It carries the organization unit's CODE and NAME because
/// <c>PositionDto</c> exposes only <c>OrganizationUnitId</c> — without the unit label a picker cannot
/// distinguish "QA Specialist — Facility A" from "QA Specialist — Facility B" and work lands in the wrong pool.
/// Joined server-side rather than reassembled client-side.
/// </summary>
/// <summary>
/// A person who may receive a task. <c>DisplayName</c> is nullable on purpose: it is resolved best-effort through
/// <c>IUserDisplayNameResolver</c>, so an AuthService outage leaves the row usable (position + unit) instead of
/// failing the whole lookup. The client renders a name-unavailable label — never the raw user id.
/// </summary>
public sealed record AssignablePersonDto(
    Guid UserId,
    string? DisplayName,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    Guid OrganizationUnitId,
    string OrganizationUnitCode,
    string OrganizationUnitName);

public sealed record AssignablePositionDto(
    Guid PositionId,
    string PositionCode,
    string PositionName,
    Guid OrganizationUnitId,
    string OrganizationUnitCode,
    string OrganizationUnitName,
    Guid LegalEntityId,
    int ActiveHolderCount);
