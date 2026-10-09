using Diten.CrmService.Application.Features.VisitReport;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

// ── reasons ────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One selectable reason of the <c>visit-outcome-reason</c> set, labelled in the request's language.</summary>
public sealed record VisitReasonDto(string Code, string Label, bool RequiresNote);

public sealed record VisitReasonListDto(
    string SetCode, string? AppliesTo, string Language, IReadOnlyList<VisitReasonDto> Items);

// ── reschedule options ───────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One day a visit can be moved to (working, inside the active period) with the rep's load on it.</summary>
public sealed record RescheduleOptionDto(
    string Date, int PlannedCount, int CapacityMinutes, int PlannedMinutes, bool IsHoliday);

public sealed record RescheduleOptionsDto(Guid PlannedVisitId, IReadOnlyList<RescheduleOptionDto> Days);

// ── unified calendar ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One card of the workspace calendar: a WRITTEN planned visit (the W1 calendar fields + the W2 additions) or a DRAFT
/// week's preview visit (<c>WorkStatus = draft</c>, <c>PlannedVisitId = null</c>, <c>PreviewKey</c>). No play /
/// campaign id (ARCH GATE).
/// </summary>
public sealed record WorkspaceVisitDto(
    Guid? PlannedVisitId,
    string? PreviewKey,
    string? VisitCode,
    string PlannedDate,
    string WeekStart,
    string? StartTime,
    string? EndTime,
    int? SequenceOrder,
    int? DurationMinutes,
    string TargetType,
    Guid TargetId,
    Guid? AccountId,
    Guid? ContactId,
    string? TargetDisplayName,
    bool TargetInactive,
    string? ResourceId,
    string? PlanStatus,
    string WorkStatus,
    DateTimeOffset? ReportDeadline,
    bool ManagerAttention,
    string ReportState,
    Guid? VisitReportId,
    string? ExecutionOutcome,
    IReadOnlyList<VisitCalendarPlannedContentDto> PlannedContent,
    string Source,
    Guid? RescheduledFromPlannedVisitId,
    Guid? RescheduledToPlannedVisitId,
    string? CancellationReason,
    string? CancellationReasonCode,
    string? CancellationNote,
    bool IsPinned,
    // W2-BE-b owns the time pin; until it lands this is always null (the field name is fixed: pinnedTime).
    string? PinnedTime,
    bool IsExtra,
    // W2-BE-c (C2, additive) — the visit's institution name (written + draft; one bulk name read).
    string? AccountDisplayName = null);

/// <summary>One Monday-week of the window: its plan state and load.</summary>
public sealed record WorkspaceWeekDto(
    string WeekStart,
    int WeekNumber,
    string State,
    Guid? PlanningSessionId,
    bool CanApprove,
    bool CanReopen,
    int CapacityMinutes,
    int PlannedMinutes,
    int VisitCount,
    int UnplacedCount,
    // W2-BE-c (C4, additive) — the version approve / reopen expect (null = no plan), and the visits that did not fit
    // (their count is UnplacedCount).
    int? SessionVersion = null,
    IReadOnlyList<WorkspaceUnplacedDto>? Unplaced = null);

/// <summary>W2-BE-c (C4) — one visit of a week that could not be placed: its target, names and the engine's reason.</summary>
public sealed record WorkspaceUnplacedDto(
    string TargetType, Guid TargetId, string? DisplayName, string? AccountDisplayName, string Reason);

/// <summary>One day of the window.</summary>
public sealed record WorkspaceDayDto(
    string Date,
    string Kind,
    bool IsHoliday,
    string? HolidayName,
    int CapacityMinutes,
    int PlannedMinutes,
    int FreeMinutes);

public sealed record WorkspaceCalendarDto(
    string From,
    string To,
    string ResourceId,
    IReadOnlyList<WorkspaceVisitDto> Visits,
    IReadOnlyList<WorkspaceWeekDto> Weeks,
    IReadOnlyList<WorkspaceDayDto> Days);

/// <summary>The workspace week states.</summary>
public static class WorkspaceWeekStates
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Past = "past";
    public const string None = "none";
}

// ── contract ─────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record VisitWorkspaceContractDto(
    string ModuleId,
    Guid TenantId,
    IReadOnlyList<string> WorkStatuses,
    string ReasonSet,
    IReadOnlyList<string> ReasonAppliesTo,
    int RescheduleOptionDays,
    int MaxWindowDays,
    int ReportDeadlineHours,
    int MaxNoteLength,
    IReadOnlyList<string> WeekStates,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions);

/// <summary>Workspace limits (one place).</summary>
public static class VisitWorkspaceLimits
{
    public const int RescheduleOptionDays = 8;
    public const int MaxWindowDays = 42;

    /// <summary>The draft-week preview status (only on the workspace calendar; the W1 vocabulary is unchanged).</summary>
    public const string DraftWorkStatus = "draft";

    /// <summary>The workspace calendar's statuses: the W1 codes in priority order, then <c>draft</c>.</summary>
    public static IReadOnlyList<string> WorkStatuses => VisitWorkStatus.All.Append(DraftWorkStatus).ToList();
}
