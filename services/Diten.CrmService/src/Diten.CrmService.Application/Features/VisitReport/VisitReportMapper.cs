using Diten.CrmService.Domain.Entities;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>Entity → DTO projections. One place, so the list, the detail and the calendar read can never disagree about
/// what a report is. RescheduleToDate is projected as an ISO "yyyy-MM-dd" string so a JSON client needs no DateOnly
/// knowledge; ExecutedAt / SubmittedAt / AmendedAt stay as instants.</summary>
public static class VisitReportMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static VisitReportListItemDto ToListItem(VisitReportEntity r) => new(
        r.Id, r.PlannedVisitId, r.ExecutionOutcome, r.ReportStatus,
        r.ContentActuals?.StageIndex, r.ContentActuals?.MatchedPlan,
        r.Feedback?.OutcomeCode, r.Feedback?.FollowUpRequired ?? false,
        r.ReportedByResourceId, r.ExecutedAt, r.SubmittedAt, r.AmendedAt,
        r.Amendments.Count, r.Version);

    public static VisitReportDetailDto ToDetail(VisitReportEntity r) => new(
        r.Id, r.PlannedVisitId, r.ExecutionOutcome, r.ReportStatus,
        ToContent(r.ContentActuals),
        r.Samples.Select(ToSample).ToList(),
        ToFeedback(r.Feedback),
        r.ReasonCode,
        r.RescheduleToDate?.ToString(DateFormat),
        r.RescheduleNotes,
        r.ReportedByResourceId, r.ExecutedAt, r.SubmittedAt, r.AmendedAt,
        r.Amendments.Select(ToAmendment).ToList(),
        r.IsDraft(), r.IsSubmitted(), r.IsAmended(),
        r.Version, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy);

    public static VisitReportContentActualsDto? ToContent(VisitReportContentActuals? c)
        => c is null
            ? null
            : new(c.JourneyId, c.StageId, c.StageIndex, c.StageCode, c.MatchedPlan,
                c.JourneyDisplayName, c.StageDisplayName);

    private static VisitReportSampleDto ToSample(VisitReportSample s)
        => new(s.ItemType, s.ItemId, s.Quantity, s.Notes);

    private static VisitReportFeedbackDto? ToFeedback(VisitReportFeedback? f)
        => f is null ? null : new(f.DoctorFeedback, f.OutcomeCode, f.FollowUpRequired, f.FollowUpNotes);

    private static VisitReportAmendmentDto ToAmendment(VisitReportAmendment a)
        => new(a.At, a.ByResourceId, a.Reason, a.ChangedFields);

    // ── write-path input → entity converters (shared by Submit + Amend so the two paths can never diverge) ─────────

    public static VisitReportContentActuals? FromInput(VisitReportContentActualsInput? input)
        => input is null
            ? null
            : new VisitReportContentActuals
            {
                JourneyId = input.JourneyId,
                StageId = input.StageId,
                StageIndex = input.StageIndex,
                StageCode = VisitReportValidation.Trim(input.StageCode),
                MatchedPlan = input.MatchedPlan ?? false,
                JourneyDisplayName = VisitReportValidation.Trim(input.JourneyDisplayName),
                StageDisplayName = VisitReportValidation.Trim(input.StageDisplayName)
            };

    public static List<VisitReportSample> FromInput(IReadOnlyList<VisitReportSampleInput>? input)
        => input is null
            ? new List<VisitReportSample>()
            : input.Select(s => new VisitReportSample
            {
                ItemType = (s.ItemType ?? string.Empty).Trim(),
                ItemId = s.ItemId,
                Quantity = s.Quantity,
                Notes = VisitReportValidation.Trim(s.Notes)
            }).ToList();

    public static VisitReportFeedback? FromInput(VisitReportFeedbackInput? input)
        => input is null
            ? null
            : new VisitReportFeedback
            {
                DoctorFeedback = VisitReportValidation.Trim(input.DoctorFeedback),
                OutcomeCode = (input.OutcomeCode ?? string.Empty).Trim(),
                FollowUpRequired = input.FollowUpRequired,
                FollowUpNotes = VisitReportValidation.Trim(input.FollowUpNotes)
            };
}
