using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Commands;

/// <summary>
/// Files an APPEND-ONLY correction to a finalised report (D-EDIT-WINDOW). TenantId is server-resolved. The report must be
/// <c>submitted</c> or <c>amended</c>; the command records a <see cref="Domain.Entities.VisitReportAmendment"/>
/// (who / when / why + the changed field names) and moves the report to <c>amended</c> — the original data the amendment
/// supersedes stays intact in the audit trail. This is NEVER a silent in-place edit: an amendment is required whenever the
/// short post-submit edit window has closed. Correcting the content/feedback is optional; the reason is mandatory.
/// </summary>
public sealed record AmendVisitReportCommand(
    Guid VisitReportId,
    string Reason,
    string? ReportedByResourceId,
    VisitReportContentActualsInput? ContentActuals,
    IReadOnlyList<VisitReportSampleInput>? Samples,
    VisitReportFeedbackInput? Feedback,
    int? ExpectedVersion,
    /// <summary>WP-VW-W2 — a reschedule date sent with an amendment. Once the reschedule created its new visit the date
    /// is frozen: a different date is 409 visit_reschedule_already_applied (the same date is a no-op).</summary>
    string? RescheduleToDate = null) : IRequest<Response<Guid>>;
