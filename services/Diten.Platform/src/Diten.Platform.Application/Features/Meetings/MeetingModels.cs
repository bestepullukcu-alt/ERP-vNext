using Diten.Platform.Domain.Enums.Meetings;

namespace Diten.Platform.Application.Features.Meetings;

// MOD-0357 S2 — ALL Meetings DTOs live in this single models file (Golden Reference Compact convention, mirrors
// MOD-0024's TaskModels.cs). Permission CONSTANTS are declared here only; the seed/grant is a separate MOD-0018
// task. The keys are attributed to Module="meetings" + Scope=Tenant through the module manifest
// (see MeetingManifestProvider) — the same reason TaskManifestProvider declares TaskPermissions instead of
// letting the A1 reflection worker stamp them Module="platform"/Scope=PlatformAdmin.

public static class MeetingPermissions
{
    public const string Read = "platform.meetings.read";
    public const string Create = "platform.meetings.create";
    public const string Update = "platform.meetings.update";
    public const string Delete = "platform.meetings.delete";
    public const string BulkDelete = "platform.meetings.bulk-delete";
    public const string MinutesWrite = "platform.meetings.minutes-write";
    public const string MinutesPublish = "platform.meetings.minutes-publish";
    public const string TypesManage = "platform.meetings.types-manage";

    /// <summary>§22 D3 — every meeting in the tenant, ignoring organizer/attendee membership (QA/management).
    /// A SECOND key, deliberately, never implied by <see cref="Read"/> — the same posture
    /// <c>TaskPermissions.WorkReportReadTenantWide</c> already takes for its own tenant-wide widening.</summary>
    public const string ReadAll = "platform.meetings.read-all";
}

public static class MeetingReasonCodes
{
    public const string NotFound = "MEETING_NOT_FOUND";
    public const string EndBeforeStart = "MEETING_END_BEFORE_START";
    public const string Cancelled = "MEETING_CANCELLED";
    public const string Completed = "MEETING_COMPLETED";
    public const string CancellationReasonRequired = "MEETING_CANCELLATION_REASON_REQUIRED";
    public const string ConcurrencyConflict = "MEETING_CONCURRENCY_CONFLICT";
    public const string SelfFollowUp = "MEETING_SELF_FOLLOW_UP";
    public const string FollowUpNotFound = "MEETING_FOLLOW_UP_NOT_FOUND";
    public const string OrganizerInvalid = "MEETING_ORGANIZER_INVALID";

    public const string AttendeeNotEligible = "MEETING_ATTENDEE_NOT_ELIGIBLE";
    public const string AttendeeDuplicate = "MEETING_ATTENDEE_DUPLICATE";
    public const string AttendeeNotFound = "MEETING_ATTENDEE_NOT_FOUND";

    public const string AgendaItemNotFound = "MEETING_AGENDA_ITEM_NOT_FOUND";
    public const string AgendaReorderMismatch = "MEETING_AGENDA_REORDER_MISMATCH";

    public const string TypeNotFound = "MEETING_TYPE_NOT_FOUND";
    public const string TypeNameDuplicate = "MEETING_TYPE_NAME_DUPLICATE";
    public const string TypeInUse = "MEETING_TYPE_IN_USE";

    // ── S4 — the meeting↔task bridge ────────────────────────────────────────────────────────────────────────
    public const string TaskAlreadyLinked = "MEETING_TASK_ALREADY_LINKED";
    public const string ReviewAlreadyScheduled = "MEETING_REVIEW_ALREADY_SCHEDULED";
}

public static class MeetingFieldLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxAgendaItemTextLength = 500;
    public const int MaxTypeNameLength = 200;
    public const int MaxAgendaTemplateLines = 20;
    public const int MaxAgendaTemplateLineLength = 200;
}

// ── Meeting ──────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateMeetingRequest(
    string Title,
    Guid MeetingTypeId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Location,
    Guid? OrganizerUserId,
    string? Description,
    Guid? FollowUpOfMeetingId,
    IReadOnlyList<Guid>? AttendeeUserIds);

public sealed record UpdateMeetingRequest(
    string Title,
    Guid MeetingTypeId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Location,
    string? Description,
    int ExpectedVersion);

public sealed record CancelMeetingRequest(string Reason, int ExpectedVersion);

public sealed record ReassignMeetingOrganizerRequest(Guid NewOrganizerUserId, int ExpectedVersion);

public sealed record MeetingDto(
    Guid Id,
    string Title,
    Guid MeetingTypeId,
    string MeetingTypeName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Location,
    Guid OrganizerUserId,
    string? Description,
    Guid? FollowUpOfMeetingId,
    MeetingLifecycle Lifecycle,
    string? CancellationReason,
    int Version,
    IReadOnlyList<MeetingAttendeeDto> Attendees,
    IReadOnlyList<AgendaItemDto> AgendaItems);

public sealed record MeetingListItemDto(
    Guid Id,
    string Title,
    Guid MeetingTypeId,
    string MeetingTypeName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid OrganizerUserId,
    MeetingLifecycle Lifecycle,
    bool IAmAttendee,
    bool HasLinkedTasks);

public sealed record GetMeetingListFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? MeetingTypeId,
    Guid? OrganizerUserId,
    bool? IAmAttendeeOnly,
    bool? HasLinkedTasksOnly,
    int Page = 1,
    int PageSize = 25);

public sealed record MeetingListResultDto(IReadOnlyList<MeetingListItemDto> Items, int TotalCount);

// ── Attendees ────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record AddMeetingAttendeesRequest(IReadOnlyList<Guid> UserIds);

public sealed record MeetingAttendeeDto(
    Guid Id,
    Guid UserId,
    string? DisplayName,
    InvitationResponse InvitationResponse,
    AttendanceStatus? AttendanceStatus);

/// <summary>Per-attendee outcome of a bulk add — K12, partial success is visible: some ids may be ineligible or
/// already-invited while others succeed, and the caller must be told which is which, never one opaque failure.</summary>
public sealed record AddMeetingAttendeesResultDto(
    IReadOnlyList<MeetingAttendeeDto> Added,
    IReadOnlyList<MeetingAttendeeSkippedDto> Skipped);

public sealed record MeetingAttendeeSkippedDto(Guid UserId, string ReasonCode);

// ── Agenda ───────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record AddAgendaItemRequest(string Text);

public sealed record UpdateAgendaItemRequest(string Text, int ExpectedVersion);

public sealed record ReorderAgendaRequest(IReadOnlyList<Guid> OrderedAgendaItemIds);

public sealed record AgendaItemDto(Guid Id, string Text, int SortOrder, int Version, Guid? RecordLinkId);

// ── Linked tasks (read-only, via RecordLink — S1's IRecordLinkService) ─────────────────────────────────────────

public sealed record LinkedTaskDto(Guid RecordLinkId, string LinkType, string TaskId, string Title, string Link);

// ── Meeting type ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CreateMeetingTypeRequest(
    string Name,
    IReadOnlyList<string>? AgendaTemplate,
    Guid? DefaultActionTaskTypeId,
    bool IsQualityRecord,
    bool RequiresESignature,
    bool AttendanceMandatory);

public sealed record UpdateMeetingTypeRequest(
    string Name,
    IReadOnlyList<string>? AgendaTemplate,
    Guid? DefaultActionTaskTypeId,
    bool IsQualityRecord,
    bool RequiresESignature,
    bool AttendanceMandatory,
    int ExpectedVersion);

public sealed record MeetingTypeDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> AgendaTemplate,
    Guid? DefaultActionTaskTypeId,
    bool IsQualityRecord,
    bool RequiresESignature,
    bool AttendanceMandatory,
    int Version);

// ── Lookups (S3 — the Create/Edit form's two pickers) ───────────────────────────────────────────────────────

/// <summary>The type dropdown's own shape — lighter than <see cref="MeetingTypeDto"/> and gated on
/// <see cref="MeetingPermissions.Read"/> rather than <see cref="MeetingPermissions.TypesManage"/>: anyone who
/// may create a meeting must be able to choose a type, without also being able to manage the type catalogue.
/// S8 — <see cref="AgendaTemplate"/> is carried here too (additive) so the Create/Details flow can pre-fill a
/// new meeting's agenda from its type (pack K8) without a second round trip through the manage-only endpoint.</summary>
public sealed record MeetingTypeLookupItemDto(Guid Id, string Name, IReadOnlyList<string> AgendaTemplate);

// ── S4 — the meeting↔task bridge (pack §3 Commands "bridge", §7, §13, K1/K2/K3/K9/K11) ────────────────────────

/// <summary>
/// "Create-and-link" — delegates to MOD-0024's own <c>CreateTaskItemCommand</c> unchanged (K2: no second create
/// path, no new field on <c>TaskItem</c>). <paramref name="TaskTypeId"/> falls back to the meeting's own
/// <c>MeetingType.DefaultActionTaskTypeId</c> when null. <paramref name="IdempotencyKey"/> is CALLER-supplied
/// (K11) — see <see cref="Services.IMeetingIdempotencyKeyResolver.ResolveForTaskBridge"/> for why this bridge
/// needs one, unlike meeting creation's own server-derived key.
/// </summary>
public sealed record CreateTaskFromMeetingRequest(
    string Title,
    string? Description,
    Guid? AssigneeUserId,
    DateTimeOffset? DueAt,
    Guid? AgendaItemId,
    Guid? TaskTypeId,
    string IdempotencyKey);

/// <summary>The bridge's own minimal result — just enough for the caller to redraw the linked-tasks list and
/// (when <paramref name="AgendaItemId"/> was supplied) know which agenda row now carries the link.</summary>
public sealed record CreateTaskFromMeetingResultDto(Guid TaskId, Guid RecordLinkId, Guid? AgendaItemId);

/// <summary>Links an EXISTING MOD-0024 task to this meeting — always <c>LinkType: "agenda"</c> (pack §17.4).
/// The task must exist and belong to this tenant, or the caller gets 404 (never a cross-tenant existence
/// leak); already-linked is 409 <see cref="MeetingReasonCodes.TaskAlreadyLinked"/>, not a silent no-op — the
/// caller asked to link a SPECIFIC task and deserves to know it already was.</summary>
public sealed record LinkExistingTaskRequest(Guid TaskId, Guid? AgendaItemId);

/// <summary>The wire body for the link-existing-task endpoint — <c>TaskId</c> travels in the ROUTE
/// (<c>POST .../tasks/{taskId}/link</c>), never duplicated in the body.</summary>
public sealed record LinkExistingTaskRequestBody(Guid? AgendaItemId);

/// <summary>
/// The receiving side of MOD-0024's <c>scheduleReviewMeeting</c> work-item action (pack §7). Delegates to the
/// SAME <c>CreateMeetingCommand</c> path a Create-page meeting uses (organizer = the acting user, the same
/// <c>MeetingEligibility</c> check) — no second meeting-creation path either. <paramref name="Title"/> defaults
/// to "Review: &lt;task title&gt;" when omitted.
/// </summary>
public sealed record ScheduleReviewMeetingForTaskRequest(
    Guid MeetingTypeId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Title,
    string IdempotencyKey);

public sealed record ScheduleReviewMeetingForTaskResultDto(Guid MeetingId, Guid RecordLinkId);
