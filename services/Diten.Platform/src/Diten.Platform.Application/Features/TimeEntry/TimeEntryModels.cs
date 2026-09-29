using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §14) — the module's own permission namespace, <c>time-entry.*</c> (PKS-001 §4 row added by
/// CT 2026-09-29). Not <c>platform.*</c>: the keys carry no service name, so extracting the module (ADR-004) needs
/// no rename. The two T4 keys (<c>team-totals.read</c>, <c>person-reports.read</c>) are minted WITH their endpoints
/// in T4 — not here.
/// </summary>
public static class TimeEntryPermissions
{
    /// <summary>Own weeks, own categories list.</summary>
    public const string TimesheetsRead = "time-entry.timesheets.read";

    /// <summary>Own drafts, submit, withdraw, correction request.</summary>
    public const string TimesheetsUpdate = "time-entry.timesheets.update";

    /// <summary>Submitted weeks routed to the caller.</summary>
    public const string ApprovalsRead = "time-entry.approvals.read";

    /// <summary>Time admin: reopen a week older than the edit window, with a reason (Tier-3, pack §14).</summary>
    public const string WeeksReopen = "time-entry.weeks.reopen";

    /// <summary>Category catalogue (Tier-3 <c>manage</c>).</summary>
    public const string CategoriesManage = "time-entry.categories.manage";

    /// <summary>Time-admin pool, legal-entity timer switch (Tier-3 <c>manage</c>).</summary>
    public const string SettingsManage = "time-entry.settings.manage";
}

/// <summary>Stable error codes (pack §12). The screen translates them in T2 (7 languages); the server never sends
/// a sentence meant to be shown.</summary>
public static class TimeEntryReasonCodes
{
    public const string StepInvalid = "TIME_ENTRY_STEP_INVALID";
    public const string FutureDate = "TIME_ENTRY_FUTURE_DATE";
    public const string DateOutsideWeek = "TIME_ENTRY_DATE_OUTSIDE_WEEK";
    public const string TargetInvalid = "TIME_ENTRY_TARGET_INVALID";
    public const string DuplicateRow = "TIME_ENTRY_DUPLICATE_ROW";
    public const string NoteTooLong = "TIME_ENTRY_NOTE_TOO_LONG";
    public const string CategoryInactive = "TIME_ENTRY_CATEGORY_INACTIVE";
    public const string DayImplausible = "TIME_ENTRY_DAY_IMPLAUSIBLE";
    public const string WeekKeyInvalid = "TIMESHEET_WEEK_KEY_INVALID";
    public const string WeekNotFound = "TIMESHEET_WEEK_NOT_FOUND";
    public const string WeekNotOpen = "TIMESHEET_WEEK_NOT_OPEN";
    public const string WeekOutsideEditWindow = "TIMESHEET_WEEK_OUTSIDE_EDIT_WINDOW";
    public const string EmptyWeek = "TIMESHEET_EMPTY_WEEK";
    public const string NoApprover = "TIMESHEET_NO_APPROVER";
    public const string ApprovalStartFailed = "TIMESHEET_APPROVAL_START_FAILED";
    public const string WithdrawTooLate = "TIMESHEET_WITHDRAW_TOO_LATE";
    public const string CorrectionReasonRequired = "TIMESHEET_CORRECTION_REASON_REQUIRED";
    public const string CorrectionAlreadyOpen = "TIMESHEET_CORRECTION_ALREADY_OPEN";
    public const string CorrectionNotAllowed = "TIMESHEET_CORRECTION_NOT_ALLOWED";
    public const string CorrectionDraftNotFound = "TIMESHEET_CORRECTION_DRAFT_NOT_FOUND";
    public const string ReopenReasonRequired = "TIMESHEET_REOPEN_REASON_REQUIRED";
    public const string ReopenNotNeeded = "TIMESHEET_REOPEN_NOT_NEEDED";
    public const string ConcurrencyConflict = "TIMESHEET_CONCURRENCY_CONFLICT";
    public const string SelfDecisionRefused = "TIMESHEET_SELF_DECISION_REFUSED";
    public const string ApprovalInstanceClosed = "TIMESHEET_APPROVAL_INSTANCE_CLOSED";
    public const string ReopenOwnWeek = "TIMESHEET_REOPEN_OWN_WEEK";
    public const string PersonNotFound = "TIMESHEET_PERSON_NOT_FOUND";
    public const string TimerSwitchConcurrencyConflict = "TIMER_SWITCH_CONCURRENCY_CONFLICT";
    public const string ApprovalWeekNotFound = "TIMESHEET_APPROVAL_NOT_FOUND";
    public const string CategoryNotFound = "WORK_CATEGORY_NOT_FOUND";
    public const string CategoryCodeInvalid = "WORK_CATEGORY_CODE_INVALID";
    public const string CategoryCodeReserved = "WORK_CATEGORY_CODE_RESERVED";
    public const string CategoryCodeDuplicate = "WORK_CATEGORY_CODE_DUPLICATE";
    public const string CategoryCodeImmutable = "WORK_CATEGORY_CODE_IMMUTABLE";
    public const string CategoryLabelRequired = "WORK_CATEGORY_LABEL_REQUIRED";
    public const string CategoryDescriptionTooLong = "WORK_CATEGORY_DESCRIPTION_TOO_LONG";
    public const string CategorySortOrderInvalid = "WORK_CATEGORY_SORT_ORDER_INVALID";
    public const string CategoryConcurrencyConflict = "WORK_CATEGORY_CONCURRENCY_CONFLICT";
    public const string SettingsPositionNotFound = "TIME_ENTRY_SETTINGS_POSITION_NOT_FOUND";
    public const string SettingsConcurrencyConflict = "TIME_ENTRY_SETTINGS_CONCURRENCY_CONFLICT";
    public const string LegalEntityRequired = "TIME_ENTRY_LEGAL_ENTITY_REQUIRED";
    public const string TimerSwitchReasonRequired = "TIMER_SWITCH_REASON_REQUIRED";

    // ── T1b — capture ───────────────────────────────────────────────────────────────────────────────────────────
    public const string TimerDisabledForLegalEntity = "TIMER_DISABLED_FOR_LEGAL_ENTITY";
    public const string TimerTaskNotHeld = "TIMER_TASK_NOT_HELD";
    public const string TimerTaskNotInProgress = "TIMER_TASK_NOT_IN_PROGRESS";
    public const string TimerNotRunning = "TIMER_NOT_RUNNING";
    public const string TimerConcurrencyConflict = "TIMER_CONCURRENCY_CONFLICT";
    public const string TimerUndoExpired = "TIMER_UNDO_EXPIRED";
    public const string SuggestionNotFound = "TIME_SUGGESTION_NOT_FOUND";
    public const string SuggestionAlreadyDecided = "TIME_SUGGESTION_ALREADY_DECIDED";
    public const string SuggestionWithdrawn = "TIME_SUGGESTION_WITHDRAWN";
    public const string SourceInvalid = "TIME_ENTRY_SOURCE_INVALID";
    public const string SourceRequired = "TIME_ENTRY_SOURCE_REQUIRED";
    public const string CapturedRowNotFound = "TIME_ENTRY_CAPTURED_ROW_NOT_FOUND";
}

/// <summary>The fixed v1 limits (pack §4.7). A tenant setting may replace them later (§20); until then they are
/// constants in ONE place.</summary>
public static class TimeEntryLimits
{
    public const int StepMinutes = 15;

    /// <summary>A single row can never exceed the implausible-day ceiling.</summary>
    public const int MaxRowMinutes = 960;

    /// <summary>A day above this is saved and submitted, but flagged to the approver (A3, Law 4857 Art. 63).</summary>
    public const int FlagDayMinutes = 660;

    /// <summary>A manual save that would take a day above this is refused (A3).</summary>
    public const int ImplausibleDayMinutes = 960;

    /// <summary>Current week plus this many previous weeks are editable without a reopen (A9).</summary>
    public const int EditWindowPreviousWeeks = 4;

    public const int NoteMaxLength = 500;
    public const int ReasonMaxLength = 1000;
    public const int CategoryDescriptionMaxLength = 500;
    public const int CategoryLabelMaxLength = 200;
    public const int ApprovalsMaxPageSize = 100;
}

/// <summary>The module's names in MOD-0023, the manifest and the audit trail.</summary>
public static class TimeEntryModule
{
    public const string ModuleCode = "time-entry";

    /// <summary>The object type this module presents to the workflow engine (and the source resolver owns).</summary>
    public const string ApprovalObjectType = "timesheet-week";

    /// <summary>The workflow definition the module installs for its own approvals.</summary>
    public const string ApprovalTemplateCode = "timesheet-approval";

    /// <summary>The audit trail's source module for every command of this module.</summary>
    public const string AuditSourceModule = "time-entry";

    /// <summary>
    /// The audit category. The platform has no time-entry category and adding one to the shared audit enum is outside
    /// this slice's enumerated edits (pack §5.1); MOD-0024's <see cref="AuditCategory.Tasks"/> is the closest existing
    /// one, and <see cref="AuditSourceModule"/> tells the two apart. Reported as an open point for CT.
    /// </summary>
    public const AuditCategory AuditCategoryValue = AuditCategory.Tasks;

    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

/// <summary>Manifest-declared notification events (pack §14). Templates are T3; T1a only declares them.</summary>
public static class TimeEntryNotificationEvents
{
    public const string WeekSubmitted = "time-entry.week.submitted";
    public const string WeekApproved = "time-entry.week.approved";
    public const string WeekRejected = "time-entry.week.rejected";
    public const string WeekWithdrawn = "time-entry.week.withdrawn";
    public const string TimerAutoClosed = "time-entry.timer.auto-closed";
}

// ── Requests ─────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One manual row of the person's draft. Exactly one of <see cref="TaskItemId"/> / <see cref="CategoryCode"/>.</summary>
public sealed record TimeEntryRowRequest(
    DateOnly LocalDate,
    Guid? TaskItemId,
    string? CategoryCode,
    int DurationMinutes,
    string? Note,
    /// <summary>REQUIRED (v2 F1): <c>Manual</c>, <c>Plan</c> (accepted from "fill from plan", D9), or <c>Timer</c> /
    /// <c>Meeting</c> for a correction of a captured row the draft already holds. A row without it is refused.</summary>
    string? Source = null,
    /// <summary>The meeting id of a <c>Meeting</c> row (it tells two meetings of one day apart); null otherwise.</summary>
    string? SourceRef = null);

/// <summary>The complete set of the person's MANUAL rows for the week's open draft. Rows left out are removed
/// (soft delete); <see cref="ExpectedVersion"/> is the week's version as last read (0 when no week exists yet).</summary>
public sealed record SaveTimeEntriesRequest(int ExpectedVersion, IReadOnlyList<TimeEntryRowRequest>? Entries);

public sealed record SubmitTimesheetWeekRequest(int ExpectedVersion);

public sealed record WithdrawTimesheetWeekRequest(int ExpectedVersion);

public sealed record RequestTimesheetCorrectionRequest(string? Reason);

/// <summary>F4 — a reopen names the person and the week, not a row id: a week that was never written has no row, and
/// reading it creates none. <see cref="ExpectedVersion"/> is 0 when the week has no revision yet.</summary>
public sealed record ReopenTimesheetWeekRequest(Guid UserId, string? WeekKey, int ExpectedVersion, string? Reason);

public sealed record CreateWorkCategoryRequest(
    string? Code,
    string? LabelText,
    string? Description,
    bool CountsAsWork,
    int SortOrder);

/// <summary><see cref="Code"/> is accepted only to be refused when it differs (the code is immutable).</summary>
public sealed record UpdateWorkCategoryRequest(
    int ExpectedVersion,
    string? Code,
    string? LabelText,
    string? Description,
    bool CountsAsWork,
    int SortOrder);

public sealed record WorkCategoryStateRequest(int ExpectedVersion);

public sealed record UpdateTimeEntrySettingsRequest(int ExpectedVersion, Guid? TimeAdminPoolPositionId);

/// <summary>Start the caller's timer on a task they hold InProgress, or on an active category. Exactly one of the two.</summary>
public sealed record StartTimerRequest(Guid? TaskItemId, string? CategoryCode);

/// <summary>Undo the switch that started the caller's running segment, within the undo window.</summary>
public sealed record UndoTimerSwitchRequest(Guid SwitchToken);

/// <summary>Accept a meeting suggestion into the week's open draft. The row lands on the named task or category; with
/// neither, on the <see cref="DefaultCategoryCode"/> category.</summary>
public sealed record AcceptTimeSuggestionRequest(int ExpectedVersion, Guid? TaskItemId, string? CategoryCode)
{
    /// <summary>One of the recommended categories (<c>InstallRecommendedWorkCategories</c>).</summary>
    public const string DefaultCategoryCode = "INTERNAL_MEETING";
}

/// <summary><see cref="ExpectedVersion"/> is the switch row's version as last read, 0 when the entity has no row (F11).</summary>
public sealed record SetLegalEntityTimerSwitchRequest(int ExpectedVersion, bool TimerEnabled, string? Reason);

// ── Responses ────────────────────────────────────────────────────────────────────────────────────────────────────
// No field here is, or is computed as, a ratio of recorded time to an estimate or a target (pack §8.7, Z-4):
// target and recorded are two durations, side by side.

public sealed record TimeEntryDto(
    Guid Id,
    DateOnly LocalDate,
    int DurationMinutes,
    Guid? TaskItemId,
    string? CategoryCode,
    string Source,
    string? Note,
    /// <summary>D3 — minutes of the timer time behind this row that fell outside the day's working window.</summary>
    int OutsideWorkingMinutes = 0,
    bool EditedFromTimer = false,
    /// <summary>D8 — an accepted meeting row whose minutes now say Absent/Excused. Shown to the person only.</summary>
    bool MinutesConflict = false,
    /// <summary>The meeting id of a Meeting row — what a correction of that row sends back (v2 F1/F8).</summary>
    string? SourceRef = null);

public sealed record TimesheetDayDto(
    DateOnly Date,
    string DayKind,
    bool IsHalfDay,
    string? HolidayName,
    int TargetMinutes,
    int RecordedMinutes,
    bool IsFlagged,
    bool IsFuture);

public sealed record InForceRevisionDto(Guid WeekId, int RevisionNumber, DateTimeOffset? ApprovedAtUtc, int TotalMinutes);

/// <summary>The person's own week. <see cref="WeekId"/> is null for a week that has never been written — and nothing
/// is written by reading it.</summary>
public sealed record TimesheetWeekDto(
    string WeekKey,
    DateOnly WeekStartDate,
    string TimeZoneId,
    DateOnly LocalToday,
    Guid? WeekId,
    int? RevisionNumber,
    string Status,
    int Version,
    bool Editable,
    string? NotEditableReason,
    bool InsideEditWindow,
    bool ReopenActive,
    string? ReopenReason,
    int? CorrectionOfRevision,
    string? CorrectionReason,
    InForceRevisionDto? InForce,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? LastRejectedAtUtc,
    string? LastRejectionReason,
    string? FinalizationBlockedReason,
    IReadOnlyList<DateOnly> FlaggedDates,
    int TotalMinutes,
    IReadOnlyList<TimesheetDayDto> Days,
    IReadOnlyList<TimeEntryDto> Entries,
    /// <summary>T1b (A2) — timer time per day and target that summed to under 8 minutes: kept, not counted.</summary>
    IReadOnlyList<TimerTooShortDto>? TooShortToCount = null,
    /// <summary>T1b (§13) — timer time on a day of this week while it was submitted or approved: kept, never added to a
    /// locked revision; the person requests a correction for it.</summary>
    IReadOnlyList<TimerOutsideOpenWeekDto>? TimerOutsideOpenWeek = null,
    /// <summary>v2 F5 — (day, target) cells whose timer draft differs from what the closed segments add up to (a close
    /// whose draft could not be written). Computed on read, never written: the next save, submit or midnight run writes
    /// it.</summary>
    IReadOnlyList<TimerDraftPendingDto>? TimerDraftPending = null,
    /// <summary>T1b (D8) — suggestions from the person's accepted meetings that ended this week.</summary>
    IReadOnlyList<TimeSuggestionDto>? Suggestions = null);

public sealed record TimerTooShortDto(DateOnly LocalDate, Guid? TaskItemId, string? CategoryCode, int Seconds);

public sealed record TimerOutsideOpenWeekDto(DateOnly LocalDate, Guid? TaskItemId, string? CategoryCode, int Minutes);

/// <summary>v2 F5 — what the segments say a cell's timer draft should be, and what the draft holds.</summary>
public sealed record TimerDraftPendingDto(DateOnly LocalDate, Guid? TaskItemId, string? CategoryCode, int SegmentMinutes, int DraftMinutes);

/// <summary>
/// One meeting suggestion (D8). <see cref="Id"/> is stable per (meeting, person) — deterministic before the person
/// decides, the stored row's id after. <see cref="MinutesStatus"/> is derived from the minutes as they stand NOW:
/// <c>confirmed</c> (Present), <c>withdrawn</c> (Absent/Excused on a suggestion not accepted — it is not offered any more),
/// <c>conflict</c> (Absent/Excused on an accepted one), or <c>none</c>.
/// </summary>
public sealed record TimeSuggestionDto(
    Guid Id,
    Guid MeetingId,
    string Title,
    DateOnly LocalDate,
    int ProposedMinutes,
    string State,
    string MinutesStatus,
    Guid? AcceptedEntryId);

/// <summary>D9 — one ghost value: what the plan block says for (day, task). Never stored; accepting it is a save with
/// <c>source: "Plan"</c>.</summary>
public sealed record PlanFillInRowDto(DateOnly LocalDate, Guid TaskItemId, int DurationMinutes);

public sealed record PlanFillInDto(string WeekKey, DateOnly LocalToday, IReadOnlyList<PlanFillInRowDto> Rows);

/// <summary>The caller's running timer segment. The browser renders elapsed time from <see cref="StartedAtUtc"/> (the
/// server's clock); it never sends an instant back.</summary>
public sealed record TimerSegmentDto(
    Guid SegmentId,
    Guid? TaskItemId,
    string? CategoryCode,
    DateTimeOffset? StartedAtUtc,
    DateOnly LocalDate,
    string StartSource,
    Guid? SwitchToken,
    DateTimeOffset? UndoUntilUtc);

/// <summary>A segment the person's local midnight closed yesterday (D3) — the morning banner.</summary>
public sealed record TimerAutoClosedDto(Guid SegmentId, DateOnly LocalDate, Guid? TaskItemId, string? CategoryCode, int DurationSeconds);

/// <summary>The caller's timer. <see cref="TimerEnabled"/> is the legal-entity switch (D12): false with
/// <see cref="DisabledReason"/> when off or unresolvable — manual entry is unaffected.</summary>
public sealed record TimerDto(
    bool TimerEnabled,
    string? DisabledReason,
    TimerSegmentDto? Running,
    IReadOnlyList<TimerAutoClosedDto> ClosedAtMidnightYesterday);

/// <summary>What a timer write answers: the timer after it, and the segment it stopped, if any.</summary>
public sealed record TimerMutationDto(TimerDto Timer, Guid? StoppedSegmentId);

/// <summary>What the person did with a suggestion.</summary>
public sealed record TimeSuggestionMutationDto(Guid SuggestionId, string State, Guid? EntryId, int? WeekVersion);

/// <summary>What a write answers: enough for the screen to carry on (the new version) without a second read.</summary>
public sealed record TimesheetWeekMutationDto(
    Guid WeekId,
    string WeekKey,
    int RevisionNumber,
    string Status,
    int Version,
    IReadOnlyList<DateOnly> FlaggedDates,
    int TotalMinutes);

public sealed record ApprovalWeekListItemDto(
    Guid WeekId,
    Guid UserId,
    string? DisplayName,
    string WeekKey,
    int RevisionNumber,
    bool IsCorrection,
    int TotalMinutes,
    IReadOnlyList<DateOnly> FlaggedDates,
    DateTimeOffset? SubmittedAtUtc,
    string? ApproverResolution);

public sealed record ApprovalWeekListDto(
    IReadOnlyList<ApprovalWeekListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>The approver's read-only view of a submitted week. There is no edit control and no edit endpoint (D6).</summary>
public sealed record ApprovalWeekDto(
    Guid WeekId,
    Guid UserId,
    string? DisplayName,
    string WeekKey,
    DateOnly WeekStartDate,
    int RevisionNumber,
    int? CorrectionOfRevision,
    string? CorrectionReason,
    string Status,
    int TotalMinutes,
    IReadOnlyList<DateOnly> FlaggedDates,
    DateTimeOffset? SubmittedAtUtc,
    Guid? WorkflowInstanceId,
    IReadOnlyList<TimesheetDayDto> Days,
    IReadOnlyList<TimeEntryDto> Entries);

public sealed record WorkCategoryDto(
    Guid Id,
    string Code,
    string? LabelText,
    string? LabelResourceKey,
    string? Description,
    bool CountsAsWork,
    int SortOrder,
    bool IsActive,
    int Version);

public sealed record InstallRecommendedWorkCategoriesResultDto(IReadOnlyList<string> Installed, IReadOnlyList<string> AlreadyPresent);

public sealed record TimeEntrySettingsDto(Guid? TimeAdminPoolPositionId, int Version);

public sealed record LegalEntityTimeSettingDto(
    Guid LegalEntityId,
    bool TimerEnabled,
    DateTimeOffset ChangedAtUtc,
    Guid ChangedByUserId,
    string? Reason,
    int Version);
