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
/// may create a meeting must be able to choose a type, without also being able to manage the type catalogue.</summary>
public sealed record MeetingTypeLookupItemDto(Guid Id, string Name);
