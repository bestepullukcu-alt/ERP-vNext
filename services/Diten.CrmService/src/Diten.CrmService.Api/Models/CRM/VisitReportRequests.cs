using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Domain.Entities;

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

    /// <summary>WP-VW-W2 — the reason note (required when the reason's requires_note).</summary>
    public string? ReasonNote { get; set; }
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

    /// <summary>WP-VW-W2 — completed (default) · missed · rescheduled (finalises the recorded draft outcome).</summary>
    public string? ExecutionOutcome { get; set; }
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

    /// <summary>WP-VW-W2 — frozen once the reschedule created its visit (409 visit_reschedule_already_applied).</summary>
    public string? RescheduleToDate { get; set; }

    /// <summary>WP-VW-W4 (W4-BE-a) — a v2 report is amended with its v2 sections (old / new values are kept).</summary>
    public VisitReportV2Request? V2 { get; set; }
}

/// <summary>WP-VW-W4 (W4-BE-a, §2.5) — the v2 draft / submit body (and the <c>v2</c> part of an amendment). The version
/// comes from <c>If-Match</c> or <see cref="ExpectedVersion"/>.</summary>
public sealed class VisitReportV2Request
{
    public Guid PlannedVisitId { get; set; }
    public VisitReportGeneral? General { get; set; }
    public List<VisitReportProductSection>? ProductSections { get; set; }
    public VisitReportDoctorLevel? Doctor { get; set; }
    public VisitReportPharmacy? Pharmacy { get; set; }
    public VisitReportSafety? Safety { get; set; }
    public VisitReportFollowUp? FollowUp { get; set; }
    public string? ReportedByResourceId { get; set; }
    public int? ExpectedVersion { get; set; }

    public VisitReportV2Body ToBody() => new(General, ProductSections, Doctor, Pharmacy, Safety, FollowUp);
}
