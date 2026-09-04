using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Commands;

/// <summary>
/// Records the EXECUTION OUTCOME of a planned visit — <c>completed</c> / <c>missed</c> / <c>rescheduled</c> (D-EXECUTION-
/// STATUS = A). TenantId is server-resolved from the claim, never accepted from a payload. It UPSERTS the draft
/// <see cref="Domain.Entities.VisitReport"/> for the plan atom (1:1 by <paramref name="PlannedVisitId"/>); the report
/// content of a completed visit is filled + finalised separately by <see cref="SubmitVisitReportCommand"/>.
/// <para><c>cancelled</c> is NOT an option here — it stays FU01's existing command, so FU02 never touches the plan's
/// PlanStatus machine. The "executed" marker reflection onto the plan atom is a documented no-op (F-EXECUTED-MARKER):
/// FU01 exposes no clean "executed" transition, so the report-side outcome is the SOLE source of truth and FU02 leaves
/// the plan atom byte-for-byte unchanged rather than forcing a semantically-wrong FU01 transition.</para>
/// </summary>
public sealed record RecordVisitOutcomeCommand(
    Guid PlannedVisitId,
    string ExecutionOutcome,
    /// <summary>Optional ISO-8601 execution instant; defaults to now when omitted.</summary>
    string? ExecutedAt,
    /// <summary>Required in-domain reason code for a <c>missed</c>/<c>rescheduled</c> outcome; forbidden for completed.</summary>
    string? ReasonCode,
    /// <summary>The new intended day for a <c>rescheduled</c> outcome (ISO "yyyy-MM-dd"). The re-plan write stays FU05's job.</summary>
    string? RescheduleToDate,
    string? RescheduleNotes,
    string? ReportedByResourceId,
    int? ExpectedVersion) : IRequest<Response<Guid>>;
