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
    string? Note);

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
    string? Note);

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
    IReadOnlyList<TimeEntryDto> Entries);

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
