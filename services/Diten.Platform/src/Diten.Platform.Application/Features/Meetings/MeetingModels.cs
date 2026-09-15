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

    /// <summary>MOD-0357 S11 — one key, same shape as <see cref="TypesManage"/> (S8): a single permission gates
    /// list/create/edit/deactivate/delete for the whole recurring-series catalogue.</summary>
    public const string SeriesManage = "platform.meetings.series-manage";
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

    /// <summary>BL-386 — the organizer's own attendee row can never be removed; reassign the meeting first
    /// (<c>ReassignMeetingOrganizerCommand</c>).</summary>
    public const string AttendeeIsOrganizer = "MEETING_ATTENDEE_IS_ORGANIZER";

    public const string AgendaItemNotFound = "MEETING_AGENDA_ITEM_NOT_FOUND";
    public const string AgendaReorderMismatch = "MEETING_AGENDA_REORDER_MISMATCH";

    public const string TypeNotFound = "MEETING_TYPE_NOT_FOUND";
    public const string TypeNameDuplicate = "MEETING_TYPE_NAME_DUPLICATE";
    public const string TypeInUse = "MEETING_TYPE_IN_USE";

    // ── S4 — the meeting↔task bridge ────────────────────────────────────────────────────────────────────────
    public const string TaskAlreadyLinked = "MEETING_TASK_ALREADY_LINKED";
    public const string ReviewAlreadyScheduled = "MEETING_REVIEW_ALREADY_SCHEDULED";

    // ── S5 — invitation response ─────────────────────────────────────────────────────────────────────────────
    /// <summary>K5 — the request body named something other than "Accept"/"Decline" ("maybe" included; there
    /// is no third state in this slice).</summary>
    public const string InvitationResponseInvalid = "MEETING_INVITATION_RESPONSE_INVALID";

    // ── S6 — minutes ──────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>K4 — the latest minutes version is Published: a draft save or a second publish against it is
    /// refused (409). The only path forward from here is <c>CorrectPublishedMinutesCommand</c>.</summary>
    public const string MinutesPublished = "MEETING_MINUTES_PUBLISHED";

    /// <summary>The inverse of <see cref="MinutesPublished"/> — a correction was requested but the meeting has
    /// no Published version to correct (nothing exists yet, or the latest row is still a Draft).</summary>
    public const string MinutesNotPublished = "MEETING_MINUTES_NOT_PUBLISHED";

    /// <summary>K4 — <c>CorrectPublishedMinutesCommand</c> with an empty/whitespace <c>CorrectionReason</c>.</summary>
    public const string MinutesCorrectionReasonRequired = "MEETING_MINUTES_CORRECTION_REASON_REQUIRED";

    /// <summary>Two edits to the same minutes DRAFT racing — same wording pattern every MOD-0024-adjacent
    /// handler's own concurrency 409 uses ("the record changed meanwhile; reload and retry").</summary>
    public const string MinutesConcurrencyConflict = "MEETING_MINUTES_CONCURRENCY_CONFLICT";

    /// <summary>The bridge's own decision-side lookup: <c>DecisionCode</c> does not name a decision on the
    /// meeting's latest minutes version.</summary>
    public const string DecisionNotFound = "MEETING_DECISION_NOT_FOUND";

    // ── S11 — recurring meeting series ───────────────────────────────────────────────────────────────────────
    public const string SeriesNotFound = "MEETING_SERIES_NOT_FOUND";
    public const string SeriesNameDuplicate = "MEETING_SERIES_NAME_DUPLICATE";
    public const string SeriesInvalidWindow = "MEETING_SERIES_INVALID_WINDOW";
    public const string SeriesIntervalInvalid = "MEETING_SERIES_INTERVAL_INVALID";
    public const string SeriesOrganizerRequired = "MEETING_SERIES_ORGANIZER_REQUIRED";
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
    IReadOnlyList<AgendaItemDto> AgendaItems,
    /// <summary>
    /// K12 (§S5) — whether the invite/change/cancel e-mail this write triggered went out. Null when this write
    /// triggers no e-mail at all (e.g. <c>GetById</c>'s own re-read) — never a stand-in for "nothing failed".
    /// </summary>
    MeetingInviteDeliveryDto? InviteDelivery = null,
    /// <summary>MOD-0357 S7 — resolved alongside <see cref="FollowUpOfMeetingId"/> for display; null when that
    /// id is null OR (rare) the source meeting could not be re-read.</summary>
    string? FollowUpOfMeetingTitle = null,
    /// <summary>K6's reverse read — the continuation THIS meeting was followed up by, if any (a derived query,
    /// never a stored field; see <c>IMeetingRepository.FindByFollowUpOfMeetingIdAsync</c>).</summary>
    Guid? FollowedByMeetingId = null,
    string? FollowedByMeetingTitle = null);

/// <summary>K12 — a failed dispatch is reported, never silently absorbed into a 201. <paramref name="Sent"/> and
/// <paramref name="Failed"/> are deliberately NOT each other's negation: no attendee to tell (organizer-only
/// meeting) is neither a send nor a failure.</summary>
public sealed record MeetingInviteDeliveryDto(bool Sent, bool Failed, string? Reason);

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
    bool HasLinkedTasks,
    /// <summary>MOD-0357 S7 — resolved from the SAME in-tenant meeting list this handler already loaded (no
    /// extra query); null when this meeting is not a continuation of another.</summary>
    Guid? FollowUpOfMeetingId = null,
    string? FollowUpOfMeetingTitle = null);

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
    AttendanceStatus? AttendanceStatus,
    /// <summary>BL-406 — true only when this attendee's meeting mail permanently failed (no further retry is
    /// coming). Never true on successful delivery or while retries are still possible; see
    /// <c>MeetingAttendee.MailUndeliveredAt</c>.</summary>
    bool MailUndelivered = false);

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

public sealed record AgendaItemDto(
    Guid Id, string Text, int SortOrder, int Version, Guid? RecordLinkId,
    /// <summary>MOD-0357 S7 (K6) — set only when this line was carried forward from a previous meeting's still-open
    /// linked tasks; see <c>AgendaItem.CarriedFromMeetingId</c>'s own doc comment for why this is not inferred
    /// from <see cref="RecordLinkId"/> alone.</summary>
    Guid? CarriedFromMeetingId = null);

// ── Linked tasks (read-only, via RecordLink — S1's IRecordLinkService) ─────────────────────────────────────────

public sealed record LinkedTaskDto(
    Guid RecordLinkId, string LinkType, string TaskId, string Title, string Link,
    /// <summary>MOD-0357 S6 (K4) — mirrors <c>RecordLink.CreatedAfterMinutesPublished</c> so the Minutes editor
    /// can label a decision's task "added later" without re-reading the link itself.</summary>
    bool CreatedAfterMinutesPublished = false);

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
    string IdempotencyKey,
    /// <summary>MOD-0357 S6 — the decision (in the meeting's LATEST minutes version) this task is "the action
    /// from". Mutually independent of <see cref="AgendaItemId"/> — a decision is not an agenda line — and
    /// optional: the two other bridge moments (preparation, on-the-spot) never send it.</summary>
    string? DecisionCode = null);

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

// ── S5 — invitation response (pack §16 K5, §13 :497, §14 :202) ─────────────────────────────────────────────────

/// <summary>
/// Exactly <c>"Accept"</c> or <c>"Decline"</c> (K5 — there is no third "maybe" state this slice). Compared
/// ordinally, case-sensitively: the wire contract names the two literal values, not a free-text choice.
/// </summary>
public sealed record RespondToInvitationRequest(string Response);

// ── S6 — minutes (pack §3/§4 "Minutes as a versioned document", K4) ─────────────────────────────────────────────

/// <summary>One attendee's presence, as the wire carries it — <c>AttendeeUserId</c> must name a real
/// <c>MeetingAttendee</c> of this meeting (validated by the handler, not here).</summary>
public sealed record MinutesAttendanceRequest(Guid AttendeeUserId, AttendanceStatus Status);

/// <summary>One decision, as the wire carries it. <c>Code</c> travels only on the READ side
/// (<see cref="MinutesDecisionDto"/>) — the write side never accepts one from the caller (server-minted, K4).</summary>
public sealed record MinutesDecisionRequest(string Text, Guid? DecidedByUserId);

/// <summary>
/// <c>SaveMinutesDraftCommand</c>'s body — the full replacement content of the CURRENT draft row.
/// <see cref="ExpectedVersion"/> is null only for the very first save of a meeting that has no minutes row yet
/// (there is nothing to be optimistic about); every subsequent save must carry the row's current
/// <c>BaseEntity.Version</c>, exactly like every other MOD-0024-adjacent draft edit.
/// </summary>
public sealed record SaveMinutesDraftRequest(
    IReadOnlyList<MinutesAttendanceRequest> Attendance,
    IReadOnlyList<MinutesDecisionRequest> Decisions,
    int? ExpectedVersion);

/// <summary><c>PublishMinutesCommand</c>'s body — the draft row's current <c>BaseEntity.Version</c>, the same
/// optimistic-concurrency token <see cref="SaveMinutesDraftRequest.ExpectedVersion"/> carries.</summary>
public sealed record PublishMinutesRequest(int ExpectedVersion);

/// <summary>
/// <c>CorrectPublishedMinutesCommand</c>'s body — K4's "the only path forward from a Published row": a mandatory
/// <see cref="CorrectionReason"/> plus the corrected content, written directly as a NEW Published version row
/// (never a second row left sitting in Draft — see the handler's own note on why).
/// </summary>
public sealed record CorrectPublishedMinutesRequest(
    IReadOnlyList<MinutesAttendanceRequest> Attendance,
    IReadOnlyList<MinutesDecisionRequest> Decisions,
    string CorrectionReason);

public sealed record MinutesAttendanceDto(Guid AttendeeUserId, string? DisplayName, AttendanceStatus Status);

public sealed record MinutesDecisionDto(string Code, string Text, Guid? DecidedByUserId, string? DecidedByDisplayName, Guid? RecordLinkId);

/// <summary>One version, as read. <see cref="ActionReferences"/> is exposed for completeness (pack §3); the
/// editor renders linked tasks per-decision, from <see cref="MinutesDecisionDto.RecordLinkId"/>, not from this
/// flat list.</summary>
public sealed record MeetingMinutesVersionDto(
    Guid Id,
    int VersionNumber,
    MinutesStatus Status,
    IReadOnlyList<MinutesAttendanceDto> Attendance,
    IReadOnlyList<MinutesDecisionDto> Decisions,
    IReadOnlyList<Guid> ActionReferences,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedByUserId,
    string? PublishedByDisplayName,
    int? CorrectionOfVersionNumber,
    string? CorrectionReason,
    int Version);

/// <summary>All versions for a meeting, newest first — the editor's own history/audit view.</summary>
public sealed record MeetingMinutesDto(IReadOnlyList<MeetingMinutesVersionDto> Versions);

// ── S7 — continuation scheduling (pack §3/§4 "Scheduling a follow-up", K6) ──────────────────────────────────────

/// <summary>
/// Schedules a NEW meeting that continues the one named in the route, via the SAME <c>CreateMeetingCommand</c>
/// path a Create-page meeting uses (K2-equivalent for meetings: no second creation path). <paramref name="Title"/>
/// defaults to "&lt;source title&gt; (devam)" when omitted; <paramref name="MeetingTypeId"/> defaults to the
/// source meeting's own type; <paramref name="OrganizerUserId"/> defaults to the source meeting's own organizer.
/// <paramref name="IdempotencyKey"/> is CALLER-supplied (K11), the same shape the S4 bridge's own requests use —
/// see <see cref="Services.IMeetingIdempotencyKeyResolver.ResolveForFollowUp"/>.
/// </summary>
public sealed record ScheduleFollowUpMeetingRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Title,
    Guid? MeetingTypeId,
    string? Location,
    Guid? OrganizerUserId,
    string? Description,
    IReadOnlyList<Guid>? AttendeeUserIds,
    string IdempotencyKey);

/// <summary>The new meeting's id plus how many of the source meeting's still-open linked-task agenda lines
/// were carried forward (K6) — enough for the caller to navigate to the new meeting and know what to expect
/// on its agenda without a second round trip.</summary>
public sealed record ScheduleFollowUpMeetingResultDto(Guid MeetingId, int CarriedAgendaItemCount);

// ── S11 — recurring meeting series (pack §19) ───────────────────────────────────────────────────────────────

public sealed record CreateMeetingSeriesRequest(
    string Name,
    Guid MeetingTypeId,
    MeetingSeriesFrequency Frequency,
    int Interval,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int DurationMinutes,
    string? Location,
    Guid OrganizerUserId,
    IReadOnlyList<Guid>? AttendeeUserIds,
    int LeadTimeDays,
    bool ChainAsFollowUp,
    bool IsActive);

public sealed record UpdateMeetingSeriesRequest(
    string Name,
    Guid MeetingTypeId,
    MeetingSeriesFrequency Frequency,
    int Interval,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int DurationMinutes,
    string? Location,
    Guid OrganizerUserId,
    IReadOnlyList<Guid>? AttendeeUserIds,
    int LeadTimeDays,
    bool ChainAsFollowUp,
    bool IsActive,
    int ExpectedVersion);

public sealed record MeetingSeriesDto(
    Guid Id,
    string Name,
    Guid MeetingTypeId,
    string? MeetingTypeName,
    MeetingSeriesFrequency Frequency,
    int Interval,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    int DurationMinutes,
    string? Location,
    Guid OrganizerUserId,
    IReadOnlyList<Guid> AttendeeUserIds,
    int LeadTimeDays,
    bool ChainAsFollowUp,
    Guid? LastGeneratedMeetingId,
    DateTimeOffset? LastGeneratedAt,
    bool IsActive,
    int Version);

/// <summary>What one sweep pass did for one tenant — same shape <c>GenerateDueRecurringTasksResponse</c>
/// already takes for MOD-0024's own recurrence sweep.</summary>
public sealed record GenerateDueMeetingSeriesResponse(
    int SeriesConsidered, int MeetingsGenerated, int AlreadyGenerated, int Failed);
