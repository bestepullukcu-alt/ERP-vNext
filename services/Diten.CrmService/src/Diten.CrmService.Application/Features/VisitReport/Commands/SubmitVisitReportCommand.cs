using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Commands;

/// <summary>
/// Records + SUBMITS the Visit Report of a COMPLETED visit (D-REPORT-PERSISTENCE = A). TenantId is server-resolved.
/// Keyed by <paramref name="PlannedVisitId"/> (1:1 with the plan atom): it fills an existing draft report or creates one,
/// sets <c>ExecutionOutcome = completed</c>, and moves the report to <c>submitted</c>.
/// <para>It captures (D-REPORT-CONTENT): the ACTUAL content presented (incl. the actual <c>StageIndex</c> + a
/// <c>MatchedPlan</c> flag — the value that closes the loop, §4.4), the outcome code (ref-data), doctor feedback,
/// samples/materials given (typed, ref-data), and a follow-up flag. FU02 writes NO advanced cursor onto the plan atom
/// (D-STAGE-ADVANCE = B) — the actual StageIndex lives on the report and is read by FU04/FU05 next cycle.</para>
/// <para><b>Immutability (D-EDIT-WINDOW).</b> Re-submitting an already-submitted report is allowed only inside the short
/// correction window; after it, the report is immutable in place and a correction must be an append-only amend (409).</para>
/// </summary>
public sealed record SubmitVisitReportCommand(
    Guid PlannedVisitId,
    VisitReportContentActualsInput? ContentActuals,
    IReadOnlyList<VisitReportSampleInput>? Samples,
    VisitReportFeedbackInput? Feedback,
    /// <summary>Optional ISO-8601 execution instant; defaults to now (or the recorded value) when omitted.</summary>
    string? ExecutedAt,
    string? ReportedByResourceId,
    int? ExpectedVersion) : IRequest<Response<Guid>>;
