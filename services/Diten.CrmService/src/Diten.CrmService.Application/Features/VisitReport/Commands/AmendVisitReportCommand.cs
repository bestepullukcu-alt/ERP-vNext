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
    int? ExpectedVersion) : IRequest<Response<Guid>>;
