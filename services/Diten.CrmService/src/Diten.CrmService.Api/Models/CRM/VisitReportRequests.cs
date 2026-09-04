using Diten.CrmService.Application.Features.VisitReport;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU02 request bodies. <c>TenantId</c> appears in none of them — it is resolved server-side from the claim.
/// The nested content / sample / feedback blocks reuse the Application input records so the wire shape and the command
/// shape can never drift.
/// </summary>
public sealed class RecordVisitOutcomeRequest
{
    public Guid PlannedVisitId { get; set; }
    public string ExecutionOutcome { get; set; } = string.Empty;

    /// <summary>Optional ISO-8601 execution instant; defaults to now.</summary>
    public string? ExecutedAt { get; set; }

    /// <summary>Required in-domain reason code for a missed/rescheduled outcome; forbidden for completed.</summary>
    public string? ReasonCode { get; set; }

    /// <summary>The new intended day for a rescheduled outcome (ISO "yyyy-MM-dd").</summary>
    public string? RescheduleToDate { get; set; }

    public string? RescheduleNotes { get; set; }
    public string? ReportedByResourceId { get; set; }
    public int? ExpectedVersion { get; set; }
}

/// <summary>Submit (record + finalise) a completed visit's report. Keyed by PlannedVisitId (1:1).</summary>
public sealed class SubmitVisitReportRequest
{
    public Guid PlannedVisitId { get; set; }
    public VisitReportContentActualsInput? ContentActuals { get; set; }
    public List<VisitReportSampleInput>? Samples { get; set; }
    public VisitReportFeedbackInput? Feedback { get; set; }
    public string? ExecutedAt { get; set; }
    public string? ReportedByResourceId { get; set; }
    public int? ExpectedVersion { get; set; }
}

/// <summary>File an append-only amendment to a finalised report (D-EDIT-WINDOW). A reason is required.</summary>
public sealed class AmendVisitReportRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? ReportedByResourceId { get; set; }
    public VisitReportContentActualsInput? ContentActuals { get; set; }
    public List<VisitReportSampleInput>? Samples { get; set; }
    public VisitReportFeedbackInput? Feedback { get; set; }
    public int? ExpectedVersion { get; set; }
}
