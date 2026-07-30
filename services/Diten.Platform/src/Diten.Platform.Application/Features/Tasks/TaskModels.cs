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

    /// <summary>Entering Waiting without saying what is being waited for.</summary>
    public const string WaitingReasonRequired = "TASK_WAITING_REASON_REQUIRED";

    /// <summary>Handing work back or on without saying why. Both are statements to another person.</summary>
    public const string HandoverReasonRequired = "TASK_HANDOVER_REASON_REQUIRED";

    /// <summary>Only the current holder may return work they were given.</summary>
    public const string ReturnNotAssignee = "TASK_RETURN_NOT_ASSIGNEE";

    /// <summary>Reassigning is the holder's or the requester's to do — nobody else's.</summary>
    public const string ReassignNotPermitted = "TASK_REASSIGN_NOT_PERMITTED";

    /// <summary>The proposed assignee is not someone work may be assigned to.</summary>
    public const string AssigneeNotAssignable = "TASK_ASSIGNEE_NOT_ASSIGNABLE";

    /// <summary>
    /// Cancelling someone else's task. A REFUSAL OF AUTHORITY, not a state conflict — an assignee who does not
    /// want the work returns it; only the requester (or administrative authority) calls it off.
    /// </summary>
    public const string CancelNotRequester = "TASK_CANCEL_NOT_REQUESTER";

    /// <summary>The workflow gate refused: approval is outstanding (MOD-0023 owns the decision).</summary>
    public const string ApprovalPending = "APPROVAL_PENDING";

    /// <summary>
    /// The workflow gate refused completion: a REVIEW is outstanding. Separate from
    /// <see cref="ApprovalPending"/> because the two gates block different acts and are cleared by different
    /// people — telling a holder "approval pending" when a reviewer is holding the work would send them to the
    /// wrong person.
    /// </summary>
    public const string ReviewPending = "REVIEW_PENDING";

    /// <summary>Review was submitted on a task that never asked for one.</summary>
    public const string ReviewNotRequired = "REVIEW_NOT_REQUIRED";

    /// <summary>
    /// A review was requested with nobody to route it to. MOD-0023 refuses to start an instance with an empty
    /// candidate list, so this is caught at the WRITE rather than left to surface as a review that will not start.
    /// </summary>
    public const string ReviewerRequired = "REVIEW_REVIEWER_REQUIRED";

    /// <summary>
    /// The review could not be OPENED — deliberately distinct from <see cref="ReviewPending"/>, which means a
    /// reviewer is holding the work. Nothing is waiting here; the handoff failed and the caller can retry. Telling
    /// someone "waiting for the reviewer" when no reviewer was ever asked sends them to wait on nobody.
    /// </summary>
    public const string ReviewStartFailed = "REVIEW_START_FAILED";

    /// <summary>A subtask may not itself have subtasks — one level only (pack §12 E2).</summary>
    public const string SubtaskDepthExceeded = "SUBTASK_DEPTH_EXCEEDED";

    /// <summary>The parent task does not exist, or belongs to another tenant.</summary>
    public const string ParentTaskNotFound = "PARENT_TASK_NOT_FOUND";

    /// <summary>The referenced checklist or task template is missing/inactive.</summary>
    public const string TemplateNotFound = "TASK_TEMPLATE_NOT_FOUND";

    /// <summary>The checklist item code does not exist on this task's run.</summary>
    public const string ChecklistItemNotFound = "CHECKLIST_ITEM_NOT_FOUND";
    public const string DependencyInvalid = "TASK_DEPENDENCY_INVALID";

    /// <summary>The other end of the edge does not exist, or belongs to another tenant.</summary>
    public const string DependencyTaskNotFound = "TASK_DEPENDENCY_TASK_NOT_FOUND";

    /// <summary>A task cannot depend on itself — a cycle of length one.</summary>
    public const string DependencySelf = "TASK_DEPENDENCY_SELF";

    /// <summary>The edge would close a cycle (A→B→A): the work could then never start.</summary>
    public const string DependencyCycle = "TASK_DEPENDENCY_CYCLE";

    /// <summary>That edge already exists; adding it twice would double every blocker sentence.</summary>
    public const string DependencyDuplicate = "TASK_DEPENDENCY_DUPLICATE";

    /// <summary>
    /// A predecessor has not reached the state its edge waits for, so this transition is refused.
    ///
    /// <para>Deliberately the SAME string as <c>WorkAggregationReasonCodes.DependencyBlocked</c>: the projection
    /// disabling the button and the handler refusing the write are one fact seen from two sides, and the client
    /// should not need two entries in its message map to say one thing. TaskDependencyTests asserts they are
    /// equal, so a rename on either side is caught rather than silently producing an unmapped code.</para>
    /// </summary>
    public const string DependencyBlocked = "DEPENDENCY_BLOCKED";

    /// <summary>
    /// A subtask is still open, so its parent cannot be completed (BL-035).
    ///
    /// <para>Same string as <c>WorkAggregationReasonCodes.SubtaskBlocked</c>, for the same reason DependencyBlocked
    /// is: the greyed button and this refusal are one fact seen from two sides, and a second spelling would need a
    /// second entry in the client's message map — the one nobody adds is the one that reaches a user raw.</para>
    /// </summary>
    public const string SubtaskBlocked = "SUBTASK_BLOCKED";

    /// <summary>
    /// The task is closed, so nothing more can be said on it. The composer is already hidden for a terminal task,
    /// but hiding a control is presentation and refusing the write is the rule — three separate gaps of exactly
    /// this shape have been closed in this module already.
    /// </summary>
    public const string CommentTaskClosed = "TASK_COMMENT_TASK_CLOSED";

    /// <summary>Empty, whitespace-only, or longer than <see cref="TaskCommentLimits.MaxTextLength"/>.</summary>
    public const string CommentTextInvalid = "TASK_COMMENT_TEXT_INVALID";

    /// <summary>
    /// No planned date was supplied — including a JSON body that omits the field, which deserializes
    /// <see cref="PlanTaskItemRequest.PlannedDate"/> to its zero value rather than throwing. Deliberately the
    /// ONLY thing this endpoint refuses: a date in the past, or one after the source due date, is a real personal
    /// plan and is accepted (see the handler for why).
    /// </summary>
    public const string PlanDateRequired = "TASK_PLAN_DATE_REQUIRED";
}

/// <summary>
/// Notification event codes this module declares in its manifest (pack §14). Email only — there is no in-app
/// channel (<c>NotificationChannelCode { Email = 0 }</c>); the header bell is BL-025.
/// </summary>
/// <summary>Shared between the handler and its tests, so the limit cannot be asserted at a value nobody enforces.</summary>
public static class TaskCommentLimits
{
    /// <summary>
    /// 2000 characters — the same ceiling the executable contract puts on a business-context text field. Long
    /// enough for a real explanation, short enough that one paste cannot bloat every read of the task.
    /// </summary>
    public const int MaxTextLength = 2000;
}

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
    IReadOnlyList<TaskWatcherRequest>? Watchers,
    // Phase 2: present when this task is created AS a subtask of another. Optional and trailing so every
    // Phase-1 caller and payload stays valid.
    Guid? ParentTaskItemId = null,
    /// <summary>Instantiate this checklist template onto the new task (pack §12 E1/E5).</summary>
    Guid? ChecklistTemplateId = null,
    /// <summary>
    /// Who the requester SUGGESTS should review — required whenever <c>ReviewRequired</c> is set, because
    /// MOD-0023 cannot start a review with nobody to route it to. A candidate hint, not a decision: MOD-0023 and
    /// MOD-0018 resolve who may actually act.
    /// </summary>
    Guid? ReviewerCandidateUserId = null);

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
    int ExpectedVersion,
    // Phase 3: approval can be switched on or off by an edit. NULL means "this caller is not editing approval" —
    // a form that never renders the toggle must not be able to silently drop an approval that is already running.
    // Trailing and optional so every Phase 1-2 payload stays valid.
    bool? ApprovalRequired = null,
    Guid? ApprovalManagerUserId = null,
    /// <summary>
    /// The reviewer candidate, on the same terms as creation. Unlike <c>ApprovalRequired</c> above this is NOT
    /// nullable-means-untouched: <c>ReviewRequired</c> is a plain bool here, so this request is a FULL REPLACE of
    /// the review requirement and the reviewer has to travel with it. An edit that drops the reviewer while the
    /// requirement stays on is refused rather than silently stripping it.
    /// </summary>
    Guid? ReviewerCandidateUserId = null);

public sealed record TaskWatcherRequest(Guid UserId, TaskWatcherRole Role, Guid? PositionId);

public sealed record TaskFieldValueDto(string DefinitionCode, TaskFieldValueType ValueType, string? Value);

public sealed record BulkDeleteTaskItemRequest(IReadOnlyList<Guid> Ids);

public sealed record ClaimTaskItemRequest(int ExpectedVersion);

public sealed record TaskTransitionRequest(int ExpectedVersion, string? ReasonCode, string? Note);

/// <summary>
/// Set (or move) a personal plan date. Its OWN request type rather than an optional field bolted onto
/// <see cref="TaskTransitionRequest"/>: the date is REQUIRED for this one transition and meaningless for the
/// other nine — same reasoning that gives <see cref="InquireTaskItemRequest"/> its own mandatory <c>Reason</c>.
/// </summary>
public sealed record PlanTaskItemRequest(int ExpectedVersion, DateTimeOffset PlannedDate);

/// <summary>
/// Park a task in Waiting. <paramref name="Reason"/> is REQUIRED and is the user's own words, so it is stored as
/// text and never routed through a resource key.
/// </summary>
public sealed record InquireTaskItemRequest(int ExpectedVersion, string Reason);

/// <summary>
/// Hand assigned work BACK to whoever asked for it. <paramref name="Reason"/> is required: a refusal the
/// requester cannot understand only moves the problem.
/// </summary>
public sealed record ReturnTaskItemRequest(int ExpectedVersion, string Reason);

/// <summary>
/// Hand work ON to somebody else. <paramref name="Reason"/> is required for the same reason as a return — the
/// new holder is being told why this is now theirs.
/// </summary>
public sealed record ReassignTaskItemRequest(int ExpectedVersion, Guid AssigneeUserId, string Reason);

/// <summary>Tick/untick one checklist item. ExpectedVersion guards the RUN, not the task.</summary>
public sealed record SetChecklistItemStateRequest(string ItemCode, bool Completed, int ExpectedVersion);

/// <summary>
/// Add an item the user typed. There is no resource key here on purpose: user text is not translatable content,
/// and routing it through a key is what puts the key itself on screen.
/// </summary>
public sealed record AddChecklistItemRequest(
    string Text,
    ChecklistItemRequirement Requirement,
    int ExpectedVersion);

/// <summary>
/// Add a typed dependency edge. Both ends are MOD-0024 tasks — a dependency on another module's object is
/// deliberately not expressible (pack §12 Y3): MOD-0024 may manage these edges only because it owns both ends.
/// </summary>
public sealed record AddTaskDependencyRequest(Guid DependsOnTaskItemId, TaskDependencyType DependencyType);

/// <summary>
/// Post a comment. Text only: no mentions are parsed, because there is no notification channel to deliver one
/// (WC-4) and a mention nobody is told about is a promise the system does not keep.
/// </summary>
public sealed record AddTaskCommentRequest(string Text);

/// <summary>Create from a template; the template supplies the shape and (optionally) the checklist.</summary>
public sealed record CreateTaskFromTemplateRequest(
    Guid TaskTemplateId,
    string? TitleOverride,
    DateTimeOffset? DueAt,
    TaskAssignmentTarget? AssignmentTargetOverride,
    Guid? AssigneeUserId,
    Guid? PoolPositionId);

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
    DateTimeOffset? UpdatedAt,
    /// <summary>
    /// The reviewer candidate, so the edit form can re-render what is already on the task. Without it the form
    /// comes back blank and the next save — a FULL REPLACE — is refused, or worse, silently strips it.
    /// </summary>
    Guid? ReviewerCandidateUserId = null,
    /// <summary>The review's MOD-0023 instance. The LINK only; the verdict is read from MOD-0023.</summary>
    Guid? ReviewWorkflowInstanceId = null);

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
