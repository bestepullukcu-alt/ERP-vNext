using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitReport.VisitReportPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU02 — VisitReport: the EXECUTION counterpart of FU05's setup console. The rep opens the Day/Week execution
/// calendar, marks a visit done/missed/rescheduled, records the immutable Visit Report, and files append-only amendments.
/// <para>There is <b>no DELETE and no bulk-delete</b> anywhere in this controller: a report is a compliance record.
/// <c>read</c> is split from <c>record</c>/<c>amend</c> (D-RBAC); <c>record</c> ALSO requires FU01
/// <c>crm.planned-visit.manage</c>. WP-VW-W1 (F-RBAC): the canonical keys are enforced; the territory fallback is gone.
/// TenantId is server-resolved from the claim.</para>
/// </summary>
[Authorize]
public sealed class VisitReportController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitReportController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-report/contract")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitReportContractQuery(), cancellationToken));

    /// <summary>The Day/Week execution calendar: FU01 plan atoms in the window joined with their FU02 report state.</summary>
    [HttpGet("api/crm/visit-report/calendar")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Calendar(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? resourceId,
        [FromQuery] string? workStatus,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitCalendarQuery(from, to, resourceId, workStatus), cancellationToken));

    [HttpGet("api/crm/visit-report")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? plannedVisitId,
        [FromQuery] string? reportStatus,
        [FromQuery] string? executionOutcome,
        [FromQuery] string? resourceId,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListVisitReportsQuery(plannedVisitId, reportStatus, executionOutcome, resourceId), cancellationToken));

    [HttpGet("api/crm/visit-report/{visitReportId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid visitReportId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetVisitReportByIdQuery(visitReportId), cancellationToken));

    /// <summary>Marks a visit done/missed/rescheduled. Requires crm.visit-report.record AND FU01 crm.planned-visit.manage.</summary>
    [HttpPost("api/crm/visit-report/outcome")]
    [HasPermission(Perms.Record)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> RecordOutcome(
        [FromBody] RecordVisitOutcomeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RecordVisitOutcomeCommand(
                request.PlannedVisitId, request.ExecutionOutcome, request.ExecutedAt, request.ReasonCode,
                request.RescheduleToDate, request.RescheduleNotes, request.ReportedByResourceId, request.ExpectedVersion,
                request.ReasonNote),
            cancellationToken));

    /// <summary>Records + submits a completed visit's report (immutable after the correction window).</summary>
    [HttpPost("api/crm/visit-report")]
    [HasPermission(Perms.Record)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitVisitReportRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SubmitVisitReportCommand(
                request.PlannedVisitId, request.ContentActuals, request.Samples, request.Feedback,
                request.ExecutedAt, request.ReportedByResourceId, request.ExpectedVersion, request.ExecutionOutcome),
            cancellationToken));

    /// <summary>Files an append-only amendment to a finalised report (D-EDIT-WINDOW). WP-VW-W4: a <c>v2</c> part amends a
    /// v2 report (old / new values recorded).</summary>
    [HttpPost("api/crm/visit-report/{visitReportId:guid}/amend")]
    [HasPermission(Perms.Amend)]
    public async Task<IActionResult> Amend(
        Guid visitReportId, [FromBody] AmendVisitReportRequest request, CancellationToken cancellationToken)
        => request.V2 is { } v2
            ? CreateActionResultInstance(await _mediator.Send(new AmendVisitReportV2Command(
                visitReportId, request.Reason, v2.ToBody(), IfMatch() ?? request.ExpectedVersion ?? v2.ExpectedVersion, request.ReportedByResourceId), cancellationToken))
            : CreateActionResultInstance(await _mediator.Send(
            new AmendVisitReportCommand(
                visitReportId, request.Reason, request.ReportedByResourceId,
                request.ContentActuals, request.Samples, request.Feedback, request.ExpectedVersion,
                request.RescheduleToDate),
            cancellationToken));

    // ---------------- WP-VW-W4 (W4-BE-a, §2.5) — report v2 ----------------

    /// <summary>Pre-fill from the execution + previous values + the doctor's profile / potentials (isDue) + indication rows
    /// + MDM competitor options + option sets + the report kind (doctor | pharmacy).</summary>
    [HttpGet("api/crm/visit-report/v2/prefill")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> PrefillV2([FromQuery] Guid plannedVisitId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitReportV2PrefillQuery(plannedVisitId), cancellationToken));

    /// <summary>Saves a v2 draft (free; the version from <c>If-Match</c> or the body).</summary>
    [HttpPut("api/crm/visit-report/v2/draft")]
    [HasPermission(Perms.Record)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> DraftV2([FromBody] VisitReportV2Request request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SaveVisitReportV2DraftCommand(request.PlannedVisitId, request.ToBody(), IfMatch() ?? request.ExpectedVersion), cancellationToken));

    /// <summary>Submits a v2 report: a missing answer → 400 <c>report_incomplete</c> with <c>data.missing[]</c>.</summary>
    [HttpPost("api/crm/visit-report/v2/submit")]
    [HasPermission(Perms.Record)]
    [HasPermission(Perms.PlannedVisitManage)]
    public async Task<IActionResult> SubmitV2([FromBody] VisitReportV2Request request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SubmitVisitReportV2Command(request.PlannedVisitId, request.ToBody(), IfMatch() ?? request.ExpectedVersion, request.ReportedByResourceId),
            cancellationToken));

    /// <summary>Competitor usage counts from the reports (no doctor identity; a cell under 5 is "&lt;5"; a merged record counts
    /// as its survivor). <c>crm.visit-report.read-aggregate</c>.</summary>
    [HttpGet("api/crm/visit-report/v2/competitor-usage")]
    [HasPermission(VisitReportV2Permissions.ReadAggregate)]
    public async Task<IActionResult> CompetitorUsage([FromQuery] string? competitorIds, [FromQuery] int? months, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        foreach (var part in (competitorIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                return CreateActionResultInstance(VisitReportV2Service.Fail<object>(VisitReportV2Errors.Invalid, "competitorIds is a comma-separated list of ids.", 400));
            }

            ids.Add(id);
        }

        return CreateActionResultInstance(await _mediator.Send(new GetCompetitorUsageQuery(ids, months), cancellationToken));
    }

    /// <summary><c>If-Match: "3"</c> / <c>W/"3"</c> / <c>3</c> → 3.</summary>
    private int? IfMatch() => DoctorInsightsController.IfMatch(Request.Headers.IfMatch.ToString());
}
