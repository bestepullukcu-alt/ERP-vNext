namespace Diten.CrmService.Application.Features.VisitReport;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU02 — every DTO / read model / command-input of the VisitReport feature, in ONE file (the single documented
// exception to the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the
// claim. ExecutedAt is surfaced as an ISO-8601 string; the reschedule intent is an ISO "yyyy-MM-dd" DateOnly string.
// ---------------------------------------------------------------------------------------------------------------

// ── read models ────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One row of the report list.</summary>
public sealed record VisitReportListItemDto(
    Guid VisitReportId,
    Guid PlannedVisitId,
    string ExecutionOutcome,
    string ReportStatus,
    int? ActualStageIndex,
    bool? MatchedPlan,
    string? OutcomeCode,
    bool FollowUpRequired,
    string ReportedByResourceId,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? AmendedAt,
    int AmendmentCount,
    int Version);

public sealed record VisitReportListDto(IReadOnlyList<VisitReportListItemDto> Items, int TotalCount);

/// <summary>The full report detail.</summary>
public sealed record VisitReportDetailDto(
    Guid VisitReportId,
    Guid PlannedVisitId,
    string ExecutionOutcome,
    string ReportStatus,
    VisitReportContentActualsDto? ContentActuals,
    IReadOnlyList<VisitReportSampleDto> Samples,
    VisitReportFeedbackDto? Feedback,
    string? ReasonCode,
    string? RescheduleToDate,
    string? RescheduleNotes,
    string ReportedByResourceId,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? AmendedAt,
    IReadOnlyList<VisitReportAmendmentDto> Amendments,
    bool IsDraft,
    bool IsSubmitted,
    bool IsAmended,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record VisitReportContentActualsDto(
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    string? StageCode,
    bool MatchedPlan,
    string? JourneyDisplayName,
    string? StageDisplayName);

public sealed record VisitReportSampleDto(string ItemType, Guid? ItemId, int Quantity, string? Notes);

public sealed record VisitReportFeedbackDto(
    string? DoctorFeedback, string OutcomeCode, bool FollowUpRequired, string? FollowUpNotes);

public sealed record VisitReportAmendmentDto(
    DateTimeOffset At, string ByResourceId, string Reason, IReadOnlyList<string> ChangedFields);

// ── calendar read (D-CALENDAR-UI = A) ──────────────────────────────────────────────────────────────────────────

/// <summary>One calendar cell: an FU01 PlannedVisit atom in the window joined with its FU02 report state.</summary>
public sealed record VisitCalendarItemDto(
    Guid PlannedVisitId,
    string VisitCode,
    string PlannedDate,
    string? PlannedStartTime,
    string? PlannedEndTime,
    int? SlotSequenceOrder,
    string? SlotStartTime,
    string TargetType,
    Guid TargetId,
    string? ResourceId,
    string PlanStatus,
    Guid? PlannedJourneyId,
    Guid? PlannedStageId,
    int? PlannedStageIndex,
    // report state (null when no report exists yet)
    Guid? VisitReportId,
    string ReportState,          // none | draft | submitted | amended
    string? ExecutionOutcome,
    int? ActualStageIndex,
    bool? MatchedPlan);

public sealed record VisitCalendarDto(
    string From, string To, IReadOnlyList<VisitCalendarItemDto> Items, int TotalCount);

// ── command inputs (embedded blocks reaching the write path) ───────────────────────────────────────────────────

public sealed record VisitReportContentActualsInput(
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    string? StageCode,
    bool? MatchedPlan,
    string? JourneyDisplayName,
    string? StageDisplayName);

public sealed record VisitReportSampleInput(string? ItemType, Guid? ItemId, int Quantity, string? Notes);

public sealed record VisitReportFeedbackInput(
    string? DoctorFeedback, string? OutcomeCode, bool FollowUpRequired, string? FollowUpNotes);
